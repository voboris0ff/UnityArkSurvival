using UnityEngine;

namespace ArkSurvival.Player
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private float _distance = 4.5f;
        [SerializeField] private float _lookHeight = 1.5f;
        [SerializeField] private float _horizontalDegreesPerPixel = 0.15f;
        [SerializeField] private float _verticalDegreesPerPixel = 0.1f;
        [SerializeField] private float _rotationSmoothing = 0f;
        [SerializeField] private float _verticalFollowTime = 0.12f;
        [SerializeField] private float _verticalFollowMaxOffset = 1f;
        [SerializeField] private float _pitchMin = -60f;
        [SerializeField] private float _pitchMax = 75f;
        [SerializeField] private float _startPitch = 15f;

        private float _yaw;
        private float _pitch;
        private float _smoothedYaw;
        private float _smoothedPitch;
        private float _pivotHeight;
        private bool _pivotReady;

        public float Yaw => _smoothedYaw;

        private void Awake()
        {
            if (_input == null)
            {
                _input = GetComponentInParent<PlayerInputReader>();
            }

            _yaw = transform.eulerAngles.y;
            _pitch = Mathf.Clamp(_startPitch, _pitchMin, _pitchMax);
            _smoothedYaw = _yaw;
            _smoothedPitch = _pitch;
        }

        private void Update()
        {
            if (_input == null)
            {
                return;
            }

            Vector2 look = _input.Look;
            _yaw += look.x * _horizontalDegreesPerPixel;
            _pitch = Mathf.Clamp(_pitch - look.y * _verticalDegreesPerPixel, _pitchMin, _pitchMax);

            if (_rotationSmoothing > 0f)
            {
                float blend = 1f - Mathf.Exp(-Time.deltaTime / _rotationSmoothing);
                _smoothedYaw = Mathf.Lerp(_smoothedYaw, _yaw, blend);
                _smoothedPitch = Mathf.Lerp(_smoothedPitch, _pitch, blend);
            }
            else
            {
                _smoothedYaw = _yaw;
                _smoothedPitch = _pitch;
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Quaternion orbitRotation = Quaternion.Euler(_smoothedPitch, _smoothedYaw, 0f);
            Vector3 targetPosition = _target.position;
            Vector3 pivot = new Vector3(targetPosition.x, FollowPivotHeight(targetPosition.y + _lookHeight), targetPosition.z);
            Vector3 position = pivot - orbitRotation * Vector3.forward * _distance;

            Vector3 lookDirection = pivot - position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                orbitRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }

            transform.SetPositionAndRotation(position, orbitRotation);
        }

        private float FollowPivotHeight(float targetHeight)
        {
            if (!_pivotReady)
            {
                _pivotHeight = targetHeight;
                _pivotReady = true;
            }
            else if (_verticalFollowTime > 0f)
            {
                float blend = 1f - Mathf.Exp(-Time.deltaTime / _verticalFollowTime);
                _pivotHeight = Mathf.Lerp(_pivotHeight, targetHeight, blend);
            }
            else
            {
                _pivotHeight = targetHeight;
            }

            if (_verticalFollowMaxOffset > 0f)
            {
                _pivotHeight = Mathf.Clamp(
                    _pivotHeight,
                    targetHeight - _verticalFollowMaxOffset,
                    targetHeight + _verticalFollowMaxOffset);
            }

            return _pivotHeight;
        }
    }
}