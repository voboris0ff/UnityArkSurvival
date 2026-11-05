using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private CharacterController _controller;
        [SerializeField] private PlayerMovementConfig _config;

        private float _verticalVelocity;
        private float _coyoteTimer;
        private float _currentSpeed;
        private Vector3 _moveDirection = Vector3.forward;
        private bool _isGrounded;
        private bool _jumpHeld;

        public PlayerMovementConfig Config => _config;
        public CharacterController Controller => _controller;
        public bool IsGrounded => _isGrounded;
        public bool CoyoteActive => _coyoteTimer > 0f;
        public bool CanJump => _isGrounded || _coyoteTimer > 0f;
        public float VerticalVelocity => _verticalVelocity;

        public float HorizontalSpeed
        {
            get
            {
                if (_controller == null)
                {
                    return 0f;
                }

                Vector3 velocity = _controller.velocity;
                return Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            }
        }

        /// <summary>
        /// Мгновенно ограничивает текущую скорость сверху (DINO-056): нужно, чтобы при
        /// взятии поленьев спринт сразу сбрасывался до шага, а не затухал через торможение.
        /// </summary>
        public void ClampSpeed(float maxSpeed)
        {
            if (maxSpeed < 0f)
            {
                return;
            }

            _currentSpeed = Mathf.Min(_currentSpeed, maxSpeed);
        }

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }
        }

        public void SetJumpHeld(bool held)
        {
            _jumpHeld = held;
        }

        public void Tick(float deltaTime)
        {
            if (_controller == null || _config == null)
            {
                return;
            }

            UpdateGroundState(deltaTime);
            ApplyGravity(deltaTime);
        }

        public void Move(Vector3 direction, float speed, float deltaTime, float accelerationScale = 1f,
            float acceleration = -1f, float deceleration = -1f)
        {
            if (_controller == null || _config == null)
            {
                return;
            }

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                if (speed > 0.0001f)
                {
                    Vector3 targetDirection = flat.normalized;
                    if (accelerationScale < 1f)
                    {
                        float turnBlend = 1f - Mathf.Exp(-_config.RotationSpeed * accelerationScale * deltaTime);
                        _moveDirection = Vector3.Slerp(_moveDirection, targetDirection, turnBlend).normalized;
                    }
                    else
                    {
                        _moveDirection = targetDirection;
                    }
                }

                Quaternion targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
                float blend = 1f - Mathf.Exp(-_config.RotationSpeed * deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            }

            float accelerationRate = acceleration >= 0f ? acceleration : _config.Acceleration;
            float decelerationRate = deceleration >= 0f ? deceleration : _config.Deceleration;
            float rate = (speed > _currentSpeed ? accelerationRate : decelerationRate)
                * Mathf.Max(0f, accelerationScale);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, speed, rate * deltaTime);

            Vector3 motion = _moveDirection * _currentSpeed + Vector3.up * _verticalVelocity;
            CollisionFlags flags = _controller.Move(motion * deltaTime);

            UpdateGroundState(0f, (flags & CollisionFlags.Below) != 0);
        }

        public void FaceDirection(Vector3 direction)
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
        }

        public void MoveInstant(Vector3 direction, float speed, float deltaTime)
        {
            _currentSpeed = speed;
            Move(direction, speed, deltaTime);
        }

        public bool TryJump()
        {
            if (!CanJump || _config == null || _controller == null)
            {
                return false;
            }

            _verticalVelocity = Mathf.Sqrt(2f * _config.Gravity * _config.JumpHeight);
            _coyoteTimer = 0f;
            _isGrounded = false;
            return true;
        }

        private void UpdateGroundState(float deltaTime, bool touchedGroundBelow = false)
        {
            bool grounded = _verticalVelocity <= 0.1f
                && (touchedGroundBelow || _controller.isGrounded || CheckGround());

            if (grounded)
            {
                _isGrounded = true;
                _coyoteTimer = _config.CoyoteTime;

                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                _isGrounded = false;
                _coyoteTimer = Mathf.Max(0f, _coyoteTimer - deltaTime);
            }
        }

        private void ApplyGravity(float deltaTime)
        {
            if (_isGrounded && _verticalVelocity < 0f)
            {
                return;
            }

            float gravity = _config.Gravity;
            if (_verticalVelocity < 0f)
            {
                gravity *= _config.FallGravityMultiplier;
            }
            else if (!_jumpHeld)
            {
                gravity *= _config.LowJumpGravityMultiplier;
            }

            _verticalVelocity = Mathf.Max(_verticalVelocity - gravity * deltaTime, -_config.MaxFallSpeed);
        }

        private bool CheckGround()
        {
            Vector3 center = transform.position + _controller.center;
            float feetOffset = _controller.height * 0.5f - _controller.radius;
            float radius = _config.GroundCheckRadius;
            Vector3 checkPosition = center - Vector3.up * feetOffset + Vector3.up * _config.GroundCheckOffset;
            Vector3 castOrigin = checkPosition + Vector3.up * radius;
            float castDistance = _config.GroundCheckOffset + radius;

            if (!Physics.SphereCast(castOrigin, radius, Vector3.down, out RaycastHit hit, castDistance,
                    _config.GroundLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return Vector3.Angle(hit.normal, Vector3.up) <= _controller.slopeLimit;
        }
    }
}