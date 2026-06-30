using Unity.Netcode;
using UnityEngine;

namespace Game.TankShooter
{
    /// <summary>
    /// Owner-authoritative top-down tank movement with classic tank controls, relative to the
    /// chassis: W/S accelerate forward/backward along the chassis' current heading, A/D rotate the
    /// chassis in place. The turret is independent and gradually rotates to face the mouse pointer.
    ///
    /// Only the owner reads input and drives the Rigidbody2D; NetworkTransform + NetworkRigidbody2D
    /// replicate the position. The prefab's NetworkTransform syncs position only, so the frame and
    /// turret angles are replicated separately via owner-written NetworkVariables (same approach as
    /// PlayerController_001's facing) and applied on the child transforms, not the rigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController_002 : PlayerController
    {
        [Header("Frame movement")]
        [Tooltip("Maximum forward speed of the chassis.")]
        [SerializeField] private float _moveSpeed = 5f;
        [Tooltip("How fast the chassis reaches its target speed (units/s^2). Higher = snappier throttle.")]
        [SerializeField] private float _acceleration = 25f;
        [Tooltip("Chassis rotation speed, in degrees/second, while steering with A/D. Lower = heavier turning.")]
        [SerializeField] private float _turnSpeed = 120f;

        [Header("Turret")]
        [Tooltip("Turret rotation speed, in degrees/second, toward the mouse pointer.")]
        [SerializeField] private float _turretRotationSpeed = 240f;

        [Header("Shooting")]
        [Tooltip("Muzzle at the cannon tip; bullets spawn here and fly toward the cursor. Place it " +
                 "clear of the tank's own collider so a shot does not detonate on the shooter.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Minimum delay between two shots, in seconds.")]
        [SerializeField] private float _fireCooldown = 0.4f;
        [Tooltip("Max angle, in degrees, between the turret and the shot direction for the cannon to fire.")]
        [SerializeField] private float _aimTolerance = 5f;

        [Header("References")]
        [SerializeField] private Rigidbody2D _rb;
        [Tooltip("Chassis/frame transform that rotates toward the movement direction.")]
        [SerializeField] private Transform _frame;
        [Tooltip("Turret transform that rotates toward the mouse, independently of the frame.")]
        [SerializeField] private Transform _turret;
        [Tooltip("Sprite renderers tinted with the lobby-picked color: frame, turret and cannon.")]
        [SerializeField] private SpriteRenderer _frameRenderer;
        [SerializeField] private SpriteRenderer _turretRenderer;
        [SerializeField] private SpriteRenderer _cannonRenderer;

        // Owner's smoothed angles (world degrees, 0 = pointing up), replicated below.
        private float _frameAngle;
        private float _turretAngle;
        private float _currentSpeed;
        private float _throttle;
        private bool _inputsEnabled = true;
        private Camera _camera;

        // Owner-only: earliest Time.time a new shot is allowed (fire cooldown).
        private float _nextFireTime;

        // Smallest angle change worth replicating, to avoid flooding the network every frame.
        private const float AngleSyncThreshold = 0.25f;

        // Owner-written orientations, mirrored to every peer (NetworkTransform syncs position only).
        private readonly NetworkVariable<float> _netFrameAngle = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> _netTurretAngle = new(writePerm: NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                _camera = Camera.main;
                _frameAngle = _frame.localEulerAngles.z;
                _turretAngle = _turret.eulerAngles.z;
            }
            else
            {
                ApplyFrameAngle(_netFrameAngle.Value);
                ApplyTurretAngle(_netTurretAngle.Value);
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                UpdateOwner();
                return;
            }

            // Non-owners are visual only: mirror the replicated orientations.
            ApplyFrameAngle(_netFrameAngle.Value);
            ApplyTurretAngle(_netTurretAngle.Value);
        }

        private void UpdateOwner()
        {
            // W/S throttle forward/backward; A/D steer the chassis left/right.
            _throttle = _inputsEnabled ? Input.GetAxisRaw("Vertical") : 0f;
            float steer = _inputsEnabled ? Input.GetAxisRaw("Horizontal") : 0f;

            // Positive steer (D) rotates the chassis clockwise (to its right); angle is CCW-positive.
            _frameAngle -= steer * _turnSpeed * Time.deltaTime;
            ApplyFrameAngle(_frameAngle);

            // Turret gradually tracks the mouse pointer, independently of the frame.
            if (_inputsEnabled)
            {
                Vector2 toPointer = (Vector2)_camera.ScreenToWorldPoint(Input.mousePosition) - (Vector2)_turret.position;
                if (toPointer.sqrMagnitude > 0.0001f)
                {
                    float targetTurretAngle = Vector2.SignedAngle(Vector2.up, toPointer);
                    _turretAngle = Mathf.MoveTowardsAngle(_turretAngle, targetTurretAngle, _turretRotationSpeed * Time.deltaTime);
                }

                TryShoot();
            }
            ApplyTurretAngle(_turretAngle);

            ReplicateAngles();
        }

        // Owner-only: while Space is held, fire from the muzzle toward the cursor, paced by the
        // cooldown and only once the turret has rotated to face the shot direction.
        private void TryShoot()
        {
            if (!Input.GetKey(KeyCode.Space) || Time.time < _nextFireTime)
                return;

            Vector2 muzzlePos = _muzzle.position;
            Vector2 toPointer = (Vector2)_camera.ScreenToWorldPoint(Input.mousePosition) - muzzlePos;
            if (toPointer.sqrMagnitude < 0.0001f)
                return;

            // Hold fire until the turret is aligned with the shot direction.
            float shotAngle = Vector2.SignedAngle(Vector2.up, toPointer);
            if (Mathf.Abs(Mathf.DeltaAngle(_turretAngle, shotAngle)) > _aimTolerance)
                return;

            _nextFireTime = Time.time + _fireCooldown;
            ShootServerRpc(muzzlePos, toPointer.normalized);
        }

        // Server spawns the networked bullet from the shared pool; it replicates to every peer.
        [ServerRpc]
        private void ShootServerRpc(Vector2 position, Vector2 direction)
        {
            BulletPooling_002.Instance.Spawn(position, direction, ColorIndex, NetworkObjectId);
        }

        private void FixedUpdate()
        {
            if (!IsOwner)
                return;

            // Throttle is signed: forward (W) or reverse (S) along the chassis heading.
            float targetSpeed = _moveSpeed * _throttle;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _acceleration * Time.fixedDeltaTime);

            Vector2 heading = Quaternion.Euler(0f, 0f, _frameAngle) * Vector2.up;
            _rb.linearVelocity = heading * _currentSpeed;
        }

        private void ApplyFrameAngle(float angle) => _frame.localRotation = Quaternion.Euler(0f, 0f, angle);

        private void ApplyTurretAngle(float angle) => _turret.rotation = Quaternion.Euler(0f, 0f, angle);

        // Only push to the network when an angle moved enough to matter.
        private void ReplicateAngles()
        {
            if (Mathf.Abs(Mathf.DeltaAngle(_netFrameAngle.Value, _frameAngle)) > AngleSyncThreshold)
                _netFrameAngle.Value = _frameAngle;
            if (Mathf.Abs(Mathf.DeltaAngle(_netTurretAngle.Value, _turretAngle)) > AngleSyncThreshold)
                _netTurretAngle.Value = _turretAngle;
        }

        protected override void ApplyColor(int colorIndex)
        {
            Color color = PlayerColors.ColorAt(colorIndex);
            _frameRenderer.color = color;
            _turretRenderer.color = color;
            _cannonRenderer.color = color;
        }

        protected override void HandleGameFinished()
        {
            // Freeze inputs everywhere; the owner also stops the physics body.
            _inputsEnabled = false;
            _throttle = 0f;

            if (IsOwner)
            {
                _currentSpeed = 0f;
                _rb.linearVelocity = Vector2.zero;
            }
        }
    }
}
