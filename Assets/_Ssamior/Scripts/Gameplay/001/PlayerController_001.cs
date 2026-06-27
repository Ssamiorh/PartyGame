using Unity.Netcode;
using UnityEngine;
using Utils;

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
    public class PlayerController_001 : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 7f;
        [SerializeField] private float _jumpForce = 12f;
        [Tooltip("Minimum delay between two consecutive jumps (ground or wall).")]
        [SerializeField] private float _jumpCooldown = 0.2f;

        [Header("Tag")]
        [Tooltip("Speed multiplier applied while this player is tagged.")]
        [SerializeField] private float _taggedSpeedMultiplier = 1.2f;
        [Tooltip("Visual shown above the player's head while it is tagged.")]
        [SerializeField] private SpriteRenderer _tagVisual;
        [Tooltip("Seconds a freshly tagged player cannot pass the tag back (prevents instant ping-pong).")]
        [SerializeField] private float _tagImmunityDuration = 0.5f;

        [Header("Sensors")]
        [Tooltip("Foot trigger sensor: reports when the player is standing on ground.")]
        [SerializeField] private TriggerSensor _groundSensor;
        [Tooltip("Side trigger sensor: reports when the player is hugging a wall.")]
        [SerializeField] private TriggerSensor _wallSensor;

        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private Animator _animator;
        [SerializeField] private OwnerNetworkAnimator _networkAnimator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [Tooltip("Separate Animator that plays the explosion when this player loses at timeout.")]
        [SerializeField] private Animator _explosionAnimator;
        private float _horizontalInput;
        private bool _jumpQueued;
        private bool _inputsEnabled = true;

        // Jump gating: earliest time a new jump is allowed, plus the single wall jump
        // that recharges only once the player touches the ground again.
        private float _nextJumpTime;
        private bool _wallJumpAvailable;

        // Animator parameter ids (must match the Animator Controller).
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int ExplodeHash = Animator.StringToHash("Explode");

        // Server-authoritative tagged flag, mirrored to every peer for visuals + speed.
        private readonly NetworkVariable<bool> _isTagged = new();

        // Owner-written facing, mirrored to every peer to flip the sprite (NetworkAnimator
        // does not replicate SpriteRenderer.flipX).
        private readonly NetworkVariable<bool> _facingLeft = new(writePerm: NetworkVariableWritePermission.Owner);

        // Owner-written color index (into PlayerColors), mirrored to every peer to tint
        // this player's sprites with their lobby-picked color.
        private readonly NetworkVariable<int> _colorIndex = new(writePerm: NetworkVariableWritePermission.Owner);

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

            _colorIndex.OnValueChanged += HandleColorChanged;
            // Owner publishes its lobby-picked color; everyone (owner included) applies it.
            if (IsOwner)
                _colorIndex.Value = PlayerColors.Clamp(
                    PlayerPrefs.GetInt(PreferencesManager.playerColor_PlayerPrefKey, 0));
            ApplyColor(_colorIndex.Value);

            _miniGame = FindAnyObjectByType<MiniGameController>();
            _miniGame.GameFinished += HandleGameFinished;
        }

        public override void OnNetworkDespawn()
        {
            _isTagged.OnValueChanged -= HandleTaggedChanged;
            _facingLeft.OnValueChanged -= HandleFacingChanged;
            _colorIndex.OnValueChanged -= HandleColorChanged;

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
            _tagVisual.gameObject.SetActive(tagged);
        }

        private void HandleFacingChanged(bool previous, bool current) => UpdateFacing(current);

        private void UpdateFacing(bool facingLeft)
        {
            _spriteRenderer.flipX = !facingLeft;
        }

        private void HandleColorChanged(int previous, int current) => ApplyColor(current);

        private void ApplyColor(int colorIndex)
        {
            Color color = PlayerColors.ColorAt(colorIndex);
            _spriteRenderer.color = color;
            _tagVisual.color = color;
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

            // Wall jumps recharge only by touching the ground again.
            if (_groundSensor.IsTouching)
                _wallJumpAvailable = true;

            if (Input.GetButtonDown("Jump") && TryConsumeJump())
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

            //If collision with something that is not a player
            if(collision.rigidbody == null)
                return;
        
            PlayerController_001 other =
                collision.collider.GetComponentInParent<PlayerController_001>();
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

            if (!targetObject.TryGetComponent<PlayerController_001>(out var target))
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

        /// <summary>
        /// Reaction when the tagged player loses at timeout. Runs on every peer (GameFinished
        /// fires everywhere and _isTagged is replicated), so the local explosion Animator is
        /// enough — no NetworkAnimator needed.
        /// </summary>
        private void Explode()
        {
            _explosionAnimator.SetTrigger(ExplodeHash);
            _spriteRenderer.color = Color.gray;
            _tagVisual.color = Color.gray;
        }

        // Owner-only: decide whether a jump is allowed right now and, if so, commit its
        // side effects. A ground jump always wins when grounded; otherwise a single wall
        // jump is allowed until the player lands again. A cooldown blocks rapid re-jumps.
        private bool TryConsumeJump()
        {
            if (Time.time < _nextJumpTime)
                return false;

            bool grounded = _groundSensor.IsTouching;
            bool canWallJump = !grounded && _wallSensor.IsTouching && _wallJumpAvailable;
            if (!grounded && !canWallJump)
                return false;

            if (canWallJump)
                _wallJumpAvailable = false;

            _nextJumpTime = Time.time + _jumpCooldown;
            return true;
        }
    }
}
