using UnityEngine;

namespace Game.OfficeGame
{
    /// <summary>
    /// Owner-authoritative top-down 2D movement for an OfficeGame player (WASD). Only the
    /// owning client reads input and drives its Rigidbody2D; NetworkTransform +
    /// NetworkRigidbody2D replicate the result to every other peer as a visual representation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController_003 : PlayerController
    {
        [SerializeField] private float _moveSpeed = 5f;
        [Tooltip("How hard the player accelerates toward its target velocity. Lower = props resist " +
                 "the push more (heavier feel); higher = snappier but bulldozes light props.")]
        [SerializeField] private float _acceleration = 25f;

        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Prop claiming")]
        [Tooltip("Props on these layers are claimed just before contact so pushing stays seamless.")]
        [SerializeField] private LayerMask _propLayers;
        [Tooltip("How far ahead (in the move direction) to claim props before actually touching them.")]
        [SerializeField] private float _claimLookAhead = 0.6f;
        [Tooltip("Radius of the look-ahead claim probe.")]
        [SerializeField] private float _claimRadius = 0.5f;

        private Vector2 _moveInput;
        private ContactFilter2D _propFilter;
        private readonly Collider2D[] _claimHits = new Collider2D[8];

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Owner-only: parent the main camera to the local player so it follows for free,
            // keeping its depth (z) offset. The body never rotates, so no rotation leaks in.
            if (IsOwner)
            {
                _propFilter = new ContactFilter2D { useTriggers = false };
                _propFilter.SetLayerMask(_propLayers);

                Transform cameraTransform = Camera.main.transform;
                float depth = cameraTransform.position.z - transform.position.z;
                cameraTransform.SetParent(transform, worldPositionStays: false);
                cameraTransform.SetLocalPositionAndRotation(new Vector3(0f, 0f, depth), Quaternion.identity);

            }
        }

        protected override void ApplyColor(int colorIndex)
        {
            _spriteRenderer.color = PlayerColors.ColorAt(colorIndex);
        }

        private void Update()
        {
            // Non-owners are just visual; their transform is driven by the network.
            if (!IsOwner)
                return;

            // "Horizontal"/"Vertical" map to A/D and W/S by default, giving WASD movement.
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        private void FixedUpdate()
        {
            if (!IsOwner)
                return;

            // Drive movement with a force toward the target velocity instead of hard-setting it,
            // so colliding props push back on the player and their mass/damping actually matter.
            Vector2 targetVelocity = _moveInput.normalized * _moveSpeed;
            _rb.AddForce((targetVelocity - _rb.linearVelocity) * _acceleration, ForceMode2D.Force);

            PreClaimAhead();
        }

        // Owner-only: claim free props just ahead of the player so ownership transfers before the
        // bodies actually touch. Without this lead a client stalls against the still-kinematic box
        // for the claim round-trip, since the box only becomes pushable once it's owned locally.
        private void PreClaimAhead()
        {
            if (_moveInput.sqrMagnitude < 0.01f)
                return;

            Vector2 probe = _rb.position + _moveInput.normalized * _claimLookAhead;
            int count = Physics2D.OverlapCircle(probe, _claimRadius, _propFilter, _claimHits);
            for (int i = 0; i < count; i++)
            {
                Prop003_Generic prop = _claimHits[i].GetComponentInParent<Prop003_Generic>();
                if (prop != null)
                    prop.RequestClaim();
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            // Owner claims any prop it leans on so the prop simulates on this client alongside the
            // player (both dynamic) and the push resolves with real physics. The prop handles the
            // tiebreak and pulls the rest of a pushed line into the cluster. Stay (not Enter) so a
            // prop is re-claimed after it auto-releases at rest while we're still touching it.
            if (!IsOwner)
                return;

            Prop003_Generic prop = collision.collider.GetComponentInParent<Prop003_Generic>();
            if (prop != null)
                prop.RequestClaim();
        }
    }
}
