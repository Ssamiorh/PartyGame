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

        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private Vector2 _moveInput;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Owner-only: parent the main camera to the local player so it follows for free,
            // keeping its depth (z) offset. The body never rotates, so no rotation leaks in.
            if (IsOwner)
            {
                Transform cameraTransform = Camera.main.transform;
                float depth = cameraTransform.position.z - transform.position.z;
                cameraTransform.SetParent(transform, worldPositionStays: false);
                cameraTransform.localPosition = new Vector3(0f, 0f, depth);
                cameraTransform.localRotation = Quaternion.identity;
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

            _rb.linearVelocity = _moveInput.normalized * _moveSpeed;
        }
    }
}
