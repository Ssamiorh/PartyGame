using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.TagPlatformer
{
    /// <summary>
    /// Owner-authoritative 2D movement for a TagPlatformer player: run left/right and jump.
    /// Only the owning client reads input and drives its Rigidbody2D; NetworkTransform +
    /// NetworkRigidbody2D replicate the result to every other peer as a visual representation.
    /// Also owns this player's "tagged" state: the tag is shown above the head, boosts speed,
    /// and is passed on by colliding with another player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController_TagPlatformer : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 7f;
        [SerializeField] private float _jumpForce = 12f;

        [Header("Tag")]
        [Tooltip("Speed multiplier applied while this player is tagged.")]
        [SerializeField] private float _taggedSpeedMultiplier = 1.2f;
        [Tooltip("Visual shown above the player's head while it is tagged.")]
        [SerializeField] private GameObject _tagVisual;
        [Tooltip("Seconds a freshly tagged player cannot pass the tag back (prevents instant ping-pong).")]
        [SerializeField] private float _tagImmunityDuration = 0.5f;
        [Tooltip("Layer(s) other players are on, so non-player collisions are skipped cheaply.")]
        [SerializeField] private LayerMask _playerLayer;

        [Header("Ground check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private float _groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask _groundLayer;
        // Reused buffer for the ground overlap query so it doesn't allocate per frame.
        private readonly List<Collider2D> _groundHits = new();

        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private Animator _animator;
        [SerializeField] private OwnerNetworkAnimator _networkAnimator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        private float _horizontalInput;
        private bool _jumpQueued;
        private bool _inputsEnabled = true;

        // Animator parameter ids (must match the Animator Controller).
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        // Server-authoritative tagged flag, mirrored to every peer for visuals + speed.
        private readonly NetworkVariable<bool> _isTagged = new();

        // Owner-written facing, mirrored to every peer to flip the sprite (NetworkAnimator
        // does not replicate SpriteRenderer.flipX).
        private readonly NetworkVariable<bool> _facingLeft = new(writePerm: NetworkVariableWritePermission.Owner);

        // ServerTime until which this player cannot pass the tag on again. Server-only.
        private double _tagImmunityEndTime;

        private MiniGameController _miniGame;

        public bool IsTagged => _isTagged.Value;

        public override void OnNetworkSpawn()
        {
            _isTagged.OnValueChanged += HandleTaggedChanged;
            UpdateTagVisual(_isTagged.Value);

            _facingLeft.OnValueChanged += HandleFacingChanged;
            UpdateFacing(_facingLeft.Value);

            _miniGame = FindAnyObjectByType<MiniGameController>();
            _miniGame.GameFinished += HandleGameFinished;
        }

        public override void OnNetworkDespawn()
        {
            _isTagged.OnValueChanged -= HandleTaggedChanged;
            _facingLeft.OnValueChanged -= HandleFacingChanged;

            _miniGame.GameFinished -= HandleGameFinished;
        }

        /// <summary>Server-only: mark this player as tagged or not.</summary>
        public void SetTagged(bool tagged)
        {
            if (!IsServer)
                return;

            _isTagged.Value = tagged;
            if (tagged)
                _tagImmunityEndTime = NetworkManager.ServerTime.Time + _tagImmunityDuration;
        }

        private void HandleTaggedChanged(bool previous, bool current) => UpdateTagVisual(current);

        private void UpdateTagVisual(bool tagged)
        {
            _tagVisual.SetActive(tagged);
        }

        private void HandleFacingChanged(bool previous, bool current) => UpdateFacing(current);

        private void UpdateFacing(bool facingLeft)
        {
            _spriteRenderer.flipX = !facingLeft;
        }

        // Owner-only: derive facing from horizontal input (kept when idle). Only writes the
        // networked value on a change so we don't flood the network every frame.
        private void UpdateFacingFromInput()
        {
            if (_horizontalInput > 0.01f && _facingLeft.Value)
                _facingLeft.Value = false;
            else if (_horizontalInput < -0.01f && !_facingLeft.Value)
                _facingLeft.Value = true;
        }

        private void Update()
        {
            // Non-owners are just visual; their transform is driven by the network.
            if (!IsOwner)
                return;

            if (!_inputsEnabled)
            {
                _horizontalInput = 0f;
                UpdateAnimator();
                return;
            }

            _horizontalInput = Input.GetAxisRaw("Horizontal");
            UpdateFacingFromInput();

            if (Input.GetButtonDown("Jump")
                && IsGrounded())
            {
                _jumpQueued = true;
                // Triggers are transient: set through NetworkAnimator so they replicate.
                _networkAnimator.SetTrigger(JumpHash);
            }

            UpdateAnimator();
        }

        // Owner-only: feed movement state into the Animator. OwnerNetworkAnimator
        // replicates the resulting parameters to every other peer.
        private void UpdateAnimator()
        {
            _animator.SetFloat(SpeedHash, Mathf.Abs(_horizontalInput));
        }

        private void FixedUpdate()
        {
            if (!IsOwner)
                return;

            float speed = _moveSpeed * (_isTagged.Value ? _taggedSpeedMultiplier : 1f);
            _rb.linearVelocity = new Vector2(_horizontalInput * speed, _rb.linearVelocity.y);

            if (_jumpQueued)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
                _rb.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
                _jumpQueued = false;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Only the owner of the currently tagged player initiates a transfer.
            if (!IsOwner || !_isTagged.Value)
                return;

            // Cheaply reject ground/walls/etc. before the component lookup.
            if ((_playerLayer.value & (1 << collision.gameObject.layer)) == 0)
                return;

            PlayerController_TagPlatformer other =
                collision.collider.GetComponentInParent<PlayerController_TagPlatformer>();
            if (other == null || other == this)
                return;

            RequestTagTransferServerRpc(other.NetworkObjectId);
        }

        // Server validates the transfer: the caller must still be tagged and past its
        // immunity window, and the target must be a live player.
        [ServerRpc]
        private void RequestTagTransferServerRpc(ulong targetNetworkObjectId)
        {
            if (!_isTagged.Value || NetworkManager.ServerTime.Time < _tagImmunityEndTime)
                return;

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject))
                return;

            PlayerController_TagPlatformer target = targetObject.GetComponent<PlayerController_TagPlatformer>();
            if (target == null)
                return;

            SetTagged(false);
            target.SetTagged(true);
        }

        private void HandleGameFinished()
        {
            // Timer is up: freeze inputs everywhere and blow up whoever is holding the tag.
            _inputsEnabled = false;
            _horizontalInput = 0f;
            _jumpQueued = false;

            if (_isTagged.Value)
                Explode();
        }

        /// <summary>Reaction when the tagged player loses at timeout. TODO: implement the explosion.</summary>
        private void Explode()
        {
        }

        private bool IsGrounded()
        {
            ContactFilter2D filter = new() { useTriggers = false };
            filter.SetLayerMask(_groundLayer);
            Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, filter, _groundHits);

            foreach (Collider2D hit in _groundHits)
            {
                // Ignore our own colliders (all share this player's Rigidbody2D); only
                // ground or other players count as standable.
                if (hit.attachedRigidbody != _rb)
                    return true;
            }

            return false;
        }
    }
}
