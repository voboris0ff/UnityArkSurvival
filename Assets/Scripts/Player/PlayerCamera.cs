using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerCamera : MonoBehaviour
    {
        private const float RollFovDuration = 0.25f;

        [SerializeField] private Transform _target;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerCameraConfig _config;

        private readonly PlayerCameraCollision _collision = new PlayerCameraCollision();

        private Camera _camera;
        private float _fovVelocity;
        private float _rollFovRemaining;
        private float _targetFov;

        private float _yaw;
        private float _pitch;
        private float _smoothedYaw;
        private float _smoothedPitch;
        private float _smoothedYawVelocity;
        private float _smoothedPitchVelocity;
        private float _pivotHeight;
        private float _pivotVelocity;
        private bool _pivotReady;
        private float _crouchAmount;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        public void SetCrouchAmount(float amount)
        {
            _crouchAmount = Mathf.Clamp01(amount);
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                Debug.LogWarning($"{nameof(PlayerCamera)}: Camera component is missing.", this);
            }

            if (_input == null)
            {
                _input = GetComponentInParent<PlayerInputReader>();
            }

            if (_config == null)
            {
                Debug.LogWarning($"{nameof(PlayerCamera)}: PlayerCameraConfig is not assigned.", this);
                return;
            }

            _yaw = transform.eulerAngles.y;
            _pitch = Mathf.Clamp(_config.StartPitch, _config.PitchMin, _config.PitchMax);
            _smoothedYaw = _yaw;
            _smoothedPitch = _pitch;
            _targetFov = _config.BaseFov;

            if (_camera != null)
            {
                _camera.fieldOfView = _config.BaseFov;
            }
        }

        private void Update()
        {
            if (_input == null || _config == null)
            {
                return;
            }

            Vector2 look = _input.Look;

            if (_input.CurrentLookDevice == LookDevice.Gamepad)
            {
                Vector2 stick = ApplyStickResponse(look);
                _yaw += stick.x * _config.StickDegreesPerSecondHorizontal * Time.deltaTime;
                _pitch -= stick.y * _config.StickDegreesPerSecondVertical * Time.deltaTime;
            }
            else
            {
                _yaw += look.x * _config.MouseDegreesPerPixelHorizontal;
                _pitch -= look.y * _config.MouseDegreesPerPixelVertical;
            }

            _pitch = Mathf.Clamp(_pitch, _config.PitchMin, _config.PitchMax);

            if (_config.RotationSmoothing > 0f)
            {
                _smoothedYaw = Mathf.SmoothDampAngle(
                    _smoothedYaw,
                    _yaw,
                    ref _smoothedYawVelocity,
                    _config.RotationSmoothing,
                    Mathf.Infinity,
                    Time.deltaTime);
                _smoothedPitch = Mathf.SmoothDampAngle(
                    _smoothedPitch,
                    _pitch,
                    ref _smoothedPitchVelocity,
                    _config.RotationSmoothing,
                    Mathf.Infinity,
                    Time.deltaTime);
            }
            else
            {
                _smoothedYaw = _yaw;
                _smoothedPitch = _pitch;
                _smoothedYawVelocity = 0f;
                _smoothedPitchVelocity = 0f;
            }

            if (_input.RollPressed)
            {
                _rollFovRemaining = RollFovDuration;
            }
            else if (_rollFovRemaining > 0f)
            {
                _rollFovRemaining = Mathf.Max(0f, _rollFovRemaining - Time.deltaTime);
            }

            bool sprinting = _input.SprintHeld && _input.Move.sqrMagnitude > 0.01f;
            _targetFov = _rollFovRemaining > 0f
                ? _config.RollFov
                : sprinting ? _config.SprintFov : _config.BaseFov;
        }

        private void LateUpdate()
        {
            if (_target == null || _config == null)
            {
                return;
            }

            Quaternion orbitRotation = Quaternion.Euler(_smoothedPitch, _smoothedYaw, 0f);
            Vector3 targetPosition = _target.position;
            float lookHeight = Mathf.Lerp(_config.LookHeight, _config.CrouchLookHeight, _crouchAmount);
            Vector3 pivot = new Vector3(
                targetPosition.x,
                FollowPivotHeight(targetPosition.y + lookHeight),
                targetPosition.z);
            Vector3 desiredPivot = pivot
                + Quaternion.Euler(0f, _smoothedYaw, 0f) * Vector3.right * _config.ShoulderOffset;
            float nearClipPlane = _camera != null ? _camera.nearClipPlane : 0.3f;
            pivot = _collision.ResolvePivot(
                pivot,
                desiredPivot,
                _config,
                _target,
                nearClipPlane);
            Vector3 position = pivot - orbitRotation * Vector3.forward * _config.Distance;
            position = _collision.Resolve(
                pivot,
                position,
                _config,
                _target,
                nearClipPlane,
                Time.deltaTime);

            Vector3 lookDirection = pivot - position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                orbitRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }

            transform.SetPositionAndRotation(position, orbitRotation);

            if (_camera != null)
            {
                _camera.fieldOfView = _config.FovSmoothTime > 0f
                    ? Mathf.SmoothDamp(
                        _camera.fieldOfView,
                        _targetFov,
                        ref _fovVelocity,
                        _config.FovSmoothTime,
                        Mathf.Infinity,
                        Time.deltaTime)
                    : _targetFov;
            }
        }

        private Vector2 ApplyStickResponse(Vector2 stick)
        {
            float magnitude = Mathf.Min(stick.magnitude, 1f);
            if (magnitude <= _config.StickDeadZone)
            {
                return Vector2.zero;
            }

            float normalized = Mathf.InverseLerp(_config.StickDeadZone, 1f, magnitude);
            AnimationCurve curve = _config.SensitivityCurve;
            float sensitivity = curve != null && curve.length > 0 ? curve.Evaluate(normalized) : normalized;

            return stick.normalized * sensitivity;
        }

        private float FollowPivotHeight(float targetHeight)
        {
            if (!_pivotReady)
            {
                _pivotHeight = targetHeight;
                _pivotVelocity = 0f;
                _pivotReady = true;
                return _pivotHeight;
            }

            if (_config.VerticalFollowTime > 0f)
            {
                _pivotHeight = Mathf.SmoothDamp(
                    _pivotHeight,
                    targetHeight,
                    ref _pivotVelocity,
                    _config.VerticalFollowTime,
                    Mathf.Infinity,
                    Time.deltaTime);
            }
            else
            {
                _pivotHeight = targetHeight;
                _pivotVelocity = 0f;
            }

            if (_config.VerticalFollowMaxOffset > 0f)
            {
                float clamped = Mathf.Clamp(
                    _pivotHeight,
                    targetHeight - _config.VerticalFollowMaxOffset,
                    targetHeight + _config.VerticalFollowMaxOffset);

                if (!Mathf.Approximately(clamped, _pivotHeight))
                {
                    _pivotHeight = clamped;
                    _pivotVelocity = 0f;
                }
            }

            return _pivotHeight;
        }
    }
}