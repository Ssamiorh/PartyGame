using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.TankShooter
{
    /// <summary>
    /// Server-authoritative tank shell. The server aims it (<see cref="Launch"/>) and drives its
    /// Rigidbody2D; NetworkTransform replicates the position to every peer as a visual representation.
    /// It explodes (despawns, returning to the pool) on contact with anything, or after a safety
    /// lifetime if it never hits. Pooled by <see cref="BulletPooling_002"/>, so the same instance is
    /// reused across shots on every peer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet_002 : NetworkBehaviour
    {
        [Tooltip("Travel speed in units/second.")]
        [SerializeField] private float _speed = 18f;
        [Tooltip("Safety despawn delay if the bullet never hits anything, in seconds.")]
        [SerializeField] private float _maxLifetime = 3f;
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Explosion")]
        [Tooltip("Animator on the explosion child (its GameObject is inactive until detonation). Its " +
                 "default state must be the non-looping explosion clip.")]
        [SerializeField] private Animator _explosionAnimator;

        // Server-only: Time.time at which the bullet self-despawns if it has not hit anything.
        private float _despawnTime;

        // Server-only: NetworkObjectId of the tank that fired this shell, so it does not detonate
        // on its own shooter (the muzzle overlaps the tank's collider at spawn).
        private ulong _shooterId;

        // Shooter's lobby color index (into PlayerColors), set by the server before spawn so the
        // tint travels in the spawn snapshot and every peer shows the right color.
        private readonly NetworkVariable<int> _colorIndex = new();

        public override void OnNetworkSpawn()
        {
            _colorIndex.OnValueChanged += HandleColorChanged;
            ApplyColor(_colorIndex.Value);
        }

        public override void OnNetworkDespawn()
        {
            _colorIndex.OnValueChanged -= HandleColorChanged;
        }

        /// <summary>Server-only: tint this bullet with the shooter's lobby color. Call before Spawn().</summary>
        public void SetColor(int colorIndex) => _colorIndex.Value = colorIndex;

        private void HandleColorChanged(int previous, int current) => ApplyColor(current);

        private void ApplyColor(int colorIndex) => _spriteRenderer.color = PlayerColors.ColorAt(colorIndex);

        /// <summary>Server-only: aim the bullet and start it moving. Call right after Spawn().</summary>
        public void Launch(Vector2 direction, ulong shooterId)
        {
            _shooterId = shooterId;
            _rb.linearVelocity = direction * _speed;
            _despawnTime = Time.time + _maxLifetime;
        }

        private void Update()
        {
            // Server owns movement and lifetime; clients just mirror it via NetworkTransform.
            if (!IsServer)
                return;

            if (Time.time >= _despawnTime)
                Explode();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsServer)
                return;

            // Don't detonate on the tank that fired this shell.
            NetworkObject hit = other.GetComponentInParent<NetworkObject>();
            if (hit != null && hit.NetworkObjectId == _shooterId)
                return;

            Explode();
        }

        // Server-only: despawn the bullet. The pool's Destroy handler then runs Detonate on every
        // peer to play the explosion before the instance is reclaimed.
        // TODO: apply damage here later.
        private void Explode()
        {
            if (IsSpawned)
                NetworkObject.Despawn();
        }

        /// <summary>
        /// Runs on every peer (called by the pool when the bullet despawns): freeze and hide the
        /// projectile, play the explosion animation, then hand the instance back via
        /// <paramref name="returnToPool"/> once the blast has finished.
        /// </summary>
        public void Detonate(Action returnToPool) => StartCoroutine(ExplosionRoutine(returnToPool));

        private IEnumerator ExplosionRoutine(Action returnToPool)
        {
            // Freeze (stop movement and collisions) and hide the projectile while the blast plays.
            _rb.simulated = false;
            _spriteRenderer.enabled = false;
            _explosionAnimator.gameObject.SetActive(true);

            // Wait one frame so the Animator enters the explosion state, then wait out its clip.
            yield return null;
            float clipLength = _explosionAnimator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(clipLength);

            // Restore the projectile state for the next time this instance is reused, then pool it.
            _explosionAnimator.gameObject.SetActive(false);
            _spriteRenderer.enabled = true;
            _rb.simulated = true;
            returnToPool();
        }
    }
}
