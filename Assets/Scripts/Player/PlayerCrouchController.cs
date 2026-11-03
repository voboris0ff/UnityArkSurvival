using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerCrouchController
    {
        private const int CeilingHitCapacity = 16;
        private const float StepSafetyMargin = 0.01f;
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
        private bool _forcedCrouch;
        private bool _rollCapsuleActive;
        private float _currentHeight;
        private float _currentCenterY;
        private float _freeHeight;
        private float _crouchAmount;

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

        public PlayerCrouchConfig Config => _config;
        public bool WantsCrouch => _wantsCrouch;
        public bool IsCrouched => _isCrouched;
        public float FreeHeight => _freeHeight;
        public float StandHeight => _standHeight;
        public float CurrentHeight => _currentHeight;
        public float StepOffset => _controller != null ? _controller.stepOffset : 0f;

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

        public float CrouchAmount => _controller != null && _config != null
            ? _crouchAmount
            : (_isCrouched ? 1f : 0f);

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

        public bool TryStand()
        {
            if (!_isCrouched)
            {
                _forcedCrouch = false;
                return true;
            }

            if (!CanStand)
            {
                return false;
            }

            _forcedCrouch = false;
            _wantsCrouch = false;
            _isCrouched = false;
            return true;
        }

        // Временный модификатор капсулы на время ролла: стойка (IsCrouched/WantsCrouch) не меняется.
        public void BeginRollCapsule()
        {
            _rollCapsuleActive = true;
        }

        // Конец ролла: если встать некуда — принудительный присед с авто-вставанием после освобождения потолка.
        public void EndRollCapsule()
        {
            _rollCapsuleActive = false;

            if (!CanStand)
            {
                ForceCrouch();
            }
        }

        // Прерывание ролла (падение): модификатор снимается, стойка остаётся текущей.
        public void CancelRollCapsule()
        {
            _rollCapsuleActive = false;
        }

        private void ForceCrouch()
        {
            _forcedCrouch = true;
            _wantsCrouch = true;
            _isCrouched = true;
        }

        public void Tick(float deltaTime)
        {
            UpdateWantsCrouch();

            if (_controller == null || _config == null)
            {
                _isCrouched = _wantsCrouch;
                _crouchAmount = _isCrouched ? 1f : 0f;
                return;
            }

            _freeHeight = MeasureFreeHeight();
            UpdateCrouchState();
            ApplyCapsule(deltaTime);
        }

        private void UpdateCrouchState()
        {
            if (_forcedCrouch)
            {
                bool holdRequested = _config.InputMode == CrouchInputMode.Hold
                    && _input != null
                    && _input.CrouchHeld;
                if (!holdRequested && CanStand)
                {
                    _forcedCrouch = false;
                    _wantsCrouch = false;
                    _isCrouched = false;
                    return;
                }

                _wantsCrouch = true;
                _isCrouched = true;
                return;
            }

            if (_wantsCrouch)
            {
                _isCrouched = true;
            }
            else if (_isCrouched && CanStand)
            {
                _isCrouched = false;
            }
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
                if (_forcedCrouch)
                {
                    _forcedCrouch = false;
                    _wantsCrouch = false;
                }
                else
                {
                    _wantsCrouch = !_wantsCrouch;
                }
            }
        }

        private void ApplyCapsule(float deltaTime)
        {
            float targetHeight = _isCrouched ? CrouchTargetHeight : _standHeight;
            if (_rollCapsuleActive)
            {
                targetHeight = Mathf.Min(targetHeight, CrouchTargetHeight);
            }

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

            ApplyStepOffset();
            UpdateCrouchAmount(deltaTime);
        }

        private void UpdateCrouchAmount(float deltaTime)
        {
            // CrouchAmount ведёт стойка, а не фактическая капсула: временное сжатие в ролле
            // и её обратный рост не должны опускать pivot камеры и Blend Tree.
            float target = _isCrouched ? 1f : 0f;
            float range = _standHeight - CrouchTargetHeight;
            float speed = Mathf.Max(0f, _config.HeightTransitionSpeed);

            if (range <= 0.0001f || speed <= 0f)
            {
                _crouchAmount = target;
                return;
            }

            _crouchAmount = Mathf.MoveTowards(_crouchAmount, target, speed / range * deltaTime);
        }

        private void ApplyStepOffset()
        {
            // CharacterController требует stepOffset меньше половины высоты капсулы:
            // у низкой капсулы большое значение даёт «залезание» на ступени и проваливание.
            float desired = _isCrouched || _rollCapsuleActive ? _config.CrouchStepOffset : _standStepOffset;
            float heightLimit = Mathf.Max(0f, _currentHeight * 0.5f - StepSafetyMargin);
            _controller.stepOffset = Mathf.Clamp(
                desired,
                0f,
                Mathf.Min(_standStepOffset, heightLimit));
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