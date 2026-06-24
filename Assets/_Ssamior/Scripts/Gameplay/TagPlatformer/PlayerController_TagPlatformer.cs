using Unity.Netcode;
using UnityEngine;

namespace Game.TagPlatformer
{
    /// <summary>
    /// Owner-authoritative 2D movement for a TagPlatformer player: run left/right and jump.
    /// Only the owning client reads input and drives its Rigidbody2D; NetworkTransform +
    /// NetworkRigidbody2D replicate the result to every other peer as a visual representation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController_TagPlatformer : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 7f;
        [SerializeField] private float _jumpForce = 12f;

        [Header("Ground check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private float _groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask _groundLayer;

        [SerializeField] private Rigidbody2D _rb;
        private float _horizontalInput;
        private bool _jumpQueued;

        private void Update()
        {
            // Non-owners are just visual; their transform is driven by the network.
            if (!IsOwner)
                return;

            _horizontalInput = Input.GetAxisRaw("Horizontal");

            if(Input.GetButtonDown("Jump"))
            {
                Debug.Log($"IsGrounded:{IsGrounded()}");
            }
            if (Input.GetButtonDown("Jump") && IsGrounded())
                _jumpQueued = true;
        }

        private void FixedUpdate()
        {
            if (!IsOwner)
                return;

            _rb.linearVelocity = new Vector2(_horizontalInput * _moveSpeed, _rb.linearVelocity.y);

            if (_jumpQueued)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
                _rb.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
                _jumpQueued = false;
            }
        }

        private bool IsGrounded()
        {
            return _groundCheck != null
                && Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayer);
        }
    }
}
