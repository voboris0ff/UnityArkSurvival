using UnityEngine;

namespace ArkSurvival.Data
{
    [CreateAssetMenu(fileName = "PlayerCameraConfig", menuName = "ArkSurvival/Player Camera Config")]
    public sealed class PlayerCameraConfig : ScriptableObject
    {
        [Header("Pose")]
        [SerializeField] private float _distance = 4.5f;
        [SerializeField] private float _lookHeight = 1.5f;
        [SerializeField] private float _crouchLookHeight = 1f;
        [SerializeField] private float _shoulderOffset = 0.5f;
        [SerializeField] private float _pitchMin = -30f;
        [SerializeField] private float _pitchMax = 55f;
        [SerializeField] private float _startPitch = 15f;

        [Header("Mouse")]
        [SerializeField] private float _mouseDegreesPerPixelHorizontal = 0.15f;
        [SerializeField] private float _mouseDegreesPerPixelVertical = 0.1f;

        [Header("Gamepad")]
        [SerializeField] private float _stickDegreesPerSecondHorizontal = 180f;
        [SerializeField] private float _stickDegreesPerSecondVertical = 120f;
        [SerializeField] private float _stickDeadZone = 0.15f;
        [SerializeField] private AnimationCurve _sensitivityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Collision")]
        [SerializeField] private float _collisionRadius = 0.2f;
        [SerializeField] private LayerMask _collisionLayerMask = 1;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _collisionReturnSpeed = 10f;

        [Header("FOV")]
        [SerializeField] private float _baseFov = 60f;
        [SerializeField] private float _sprintFov = 68f;
        [SerializeField] private float _rollFov = 72f;
        [SerializeField] private float _fovSmoothTime = 0.15f;

        [Header("Smoothing")]
        [SerializeField] private float _rotationSmoothing = 0f;
        [SerializeField] private float _verticalFollowTime = 0.12f;
        [SerializeField] private float _verticalFollowMaxOffset = 1f;

        public float Distance => _distance;
        public float LookHeight => _lookHeight;
        public float CrouchLookHeight => _crouchLookHeight;
        public float ShoulderOffset => _shoulderOffset;
        public float PitchMin => _pitchMin;
        public float PitchMax => _pitchMax;
        public float StartPitch => _startPitch;
        public float MouseDegreesPerPixelHorizontal => _mouseDegreesPerPixelHorizontal;
        public float MouseDegreesPerPixelVertical => _mouseDegreesPerPixelVertical;
        public float StickDegreesPerSecondHorizontal => _stickDegreesPerSecondHorizontal;
        public float StickDegreesPerSecondVertical => _stickDegreesPerSecondVertical;
        public float StickDeadZone => _stickDeadZone;
        public AnimationCurve SensitivityCurve => _sensitivityCurve;
        public float CollisionRadius => _collisionRadius;
        public LayerMask CollisionLayerMask => _collisionLayerMask;
        public float MinDistance => _minDistance;
        public float CollisionReturnSpeed => _collisionReturnSpeed;
        public float BaseFov => _baseFov;
        public float SprintFov => _sprintFov;
        public float RollFov => _rollFov;
        public float FovSmoothTime => _fovSmoothTime;
        public float RotationSmoothing => _rotationSmoothing;
        public float VerticalFollowTime => _verticalFollowTime;
        public float VerticalFollowMaxOffset => _verticalFollowMaxOffset;
    }
}