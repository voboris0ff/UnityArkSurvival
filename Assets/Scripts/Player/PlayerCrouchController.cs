using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerCrouchController
    {
        private const int CeilingHitCapacity = 16;
        private static readonly RaycastHit[] CeilingHits = new RaycastHit[CeilingHitCapacity];

        private readonly PlayerInputReader _input;
        private readonly PlayerCrouchConfig _config;
        private readonly CharacterController _controller;
        private readonly Transform _playerTransform;

        private readonly float _standHeight;
        private readonly float _standCenterY;
        private readonly float _standBottom;
        private readonly float _standStepOffset;

        private bool _wantsCrouch;
        private bool _isCrouched;
        private float _currentHeight;
        private float _currentCenterY;
        private float _freeHeight;

        public PlayerCrouchController(
            PlayerInputReader input,
            PlayerCrouchConfig config,
            CharacterController controller,
            Transform playerTransform)
        {
            _input = input;
            _config = config;
            _controller = controller;
            _playerTransform = playerTransform;

            if (_controller != null)
            {
                _standHeight = _controller.height;
                _standCenterY = _controller.center.y;
                _standBottom = _standCenterY - _standHeight * 0.5f;
                _standStepOffset = _controller.stepOffset;
                _currentHeight = _standHeight;
                _currentCenterY = _standCenterY;
                _freeHeight = _standHeight;
            }
        }

        public bool WantsCrouch => _wantsCrouch;
        public bool IsCrouched => _isCrouched;
        public float FreeHeight => _freeHeight;

        public bool CanStand
        {
            get
            {
                if (_controller == null || _config == null)
                {
                    return true;
                }

                return _freeHeight >= _standHeight - 0.001f;
            }
        }

        public float CrouchAmount
        {
            get
            {
                float crouchHeight = CrouchTargetHeight;
                float range = _standHeight - crouchHeight;
                if (range <= 0.0001f)
                {
                    return _isCrouched ? 1f : 0f;
                }

                return Mathf.Clamp01((_standHeight - _currentHeight) / range);
            }
        }

        private float CrouchTargetHeight
        {
            get
            {
                if (_config == null || _controller == null)
                {
                    return _standHeight;
                }

                float minHeight = Mathf.Min(_controller.radius * 2f, _standHeight);
                float target = _config.CrouchHeight;
                float centerHeight = 2f * (_config.CrouchCenterY - _standBottom);
                if (centerHeight > 0.001f)
                {
                    target = Mathf.Min(target, centerHeight);
                }

                return Mathf.Clamp(target, minHeight, _standHeight);
            }
        }

        public void Tick(float deltaTime)
        {
            UpdateWantsCrouch();

            if (_controller == null || _config == null)
            {
                _isCrouched = _wantsCrouch;
                return;
            }

            _freeHeight = MeasureFreeHeight();

            if (_wantsCrouch)
            {
                _isCrouched = true;
            }
            else if (_isCrouched && CanStand)
            {
                _isCrouched = false;
            }

            ApplyCapsule(deltaTime);
        }

        private void UpdateWantsCrouch()
        {
            CrouchInputMode mode = _config != null ? _config.InputMode : CrouchInputMode.Toggle;

            if (mode == CrouchInputMode.Hold)
            {
                _wantsCrouch = _input != null && _input.CrouchHeld;
                return;
            }

            if (_input != null && _input.CrouchPressed)
            {
                _wantsCrouch = !_wantsCrouch;
            }
        }

        private void ApplyCapsule(float deltaTime)
        {
            float targetHeight = _isCrouched ? CrouchTargetHeight : _standHeight;
            if (targetHeight > _currentHeight)
            {
                targetHeight = Mathf.Min(targetHeight, Mathf.Max(_currentHeight, _freeHeight));
            }

            float speed = Mathf.Max(0f, _config.HeightTransitionSpeed) * deltaTime;

            _currentHeight = Mathf.MoveTowards(_currentHeight, targetHeight, speed);
            _currentCenterY = _standBottom + _currentHeight * 0.5f;

            _controller.height = _currentHeight;
            Vector3 center = _controller.center;
            center.y = _currentCenterY;
            _controller.center = center;

            float desiredStep = _isCrouched ? _config.CrouchStepOffset : _standStepOffset;
            _controller.stepOffset = Mathf.Clamp(desiredStep, 0f, Mathf.Min(_standStepOffset, _currentHeight * 0.5f));
        }

        private float MeasureFreeHeight()
        {
            float radius = _controller.radius;
            if (radius <= 0f)
            {
                return _standHeight;
            }

            float skin = Mathf.Max(0.01f, _config.CeilingSkin);
            float castRadius = Mathf.Max(0.01f, radius - skin);
            float lift = skin;

            Vector3 feet = (_playerTransform != null ? _playerTransform : _controller.transform).position
                + Vector3.up * _standBottom;
            Vector3 origin = feet + Vector3.up * (castRadius + lift);
            float castDistance = Mathf.Max(0f, _standHeight - castRadius * 2f - lift) + skin;

            int count = Physics.SphereCastNonAlloc(
                origin,
                castRadius,
                Vector3.up,
                CeilingHits,
                castDistance,
                _config.CeilingLayerMask,
                QueryTriggerInteraction.Ignore);

            float freeHeight = _standHeight;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = CeilingHits[i];
                if (hit.collider == null || IsPlayerCollider(hit.collider))
                {
                    continue;
                }

                float hitFreeHeight = hit.distance + castRadius * 2f + lift - skin;
                freeHeight = Mathf.Min(freeHeight, hitFreeHeight);
            }

            return Mathf.Clamp(freeHeight, 0f, _standHeight);
        }

        private bool IsPlayerCollider(Collider collider)
        {
            Transform reference = _playerTransform != null ? _playerTransform : _controller.transform;
            return collider.transform == reference || collider.transform.IsChildOf(reference);
        }
    }
}