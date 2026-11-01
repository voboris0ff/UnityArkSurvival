using UnityEngine;

namespace ArkSurvival.Data
{
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "ArkSurvival/Player Movement Config")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Locomotion")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _acceleration = 30f;
        [SerializeField] private float _deceleration = 35f;
        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Jump & Gravity")]
        [SerializeField] private float _jumpHeight = 1.6f;
        [SerializeField] private float _gravity = 25f;
        [SerializeField] private float _fallGravityMultiplier = 1.8f;
        [SerializeField] private float _lowJumpGravityMultiplier = 2.2f;
        [SerializeField] private float _maxFallSpeed = 50f;
        [SerializeField] private float _airControl = 0.5f;
        [SerializeField] private float _coyoteTime = 0.15f;
        [SerializeField] private float _jumpBufferTime = 0.12f;

        [Header("Roll")]
        [SerializeField] private AnimationCurve _rollSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
        [SerializeField] private float _rollSpeed = 8f;
        [SerializeField] private float _rollDuration = 0.45f;
        [SerializeField] private float _rollCooldown = 0.6f;
        [SerializeField] private float _iFramesStart = 0.05f;
        [SerializeField] private float _iFramesEnd = 0.35f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask _groundLayerMask;
        [SerializeField] private float _groundCheckRadius = 0.3f;
        [SerializeField] private float _groundCheckOffset = 0.1f;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float Acceleration => _acceleration;
        public float Deceleration => _deceleration;
        public float RotationSpeed => _rotationSpeed;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float FallGravityMultiplier => _fallGravityMultiplier;
        public float LowJumpGravityMultiplier => _lowJumpGravityMultiplier;
        public float MaxFallSpeed => _maxFallSpeed;
        public float AirControl => _airControl;
        public float CoyoteTime => _coyoteTime;
        public float JumpBufferTime => _jumpBufferTime;
        public AnimationCurve RollSpeedCurve => _rollSpeedCurve;
        public float RollSpeed => _rollSpeed;
        public float RollDuration => _rollDuration;
        public float RollCooldown => _rollCooldown;
        public float IFramesStart => _iFramesStart;
        public float IFramesEnd => _iFramesEnd;
        public LayerMask GroundLayerMask => _groundLayerMask;
        public float GroundCheckRadius => _groundCheckRadius;
        public float GroundCheckOffset => _groundCheckOffset;
    }
}