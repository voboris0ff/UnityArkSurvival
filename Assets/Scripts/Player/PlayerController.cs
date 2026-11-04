using ArkSurvival.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArkSurvival.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private PlayerStaminaConfig _staminaConfig;
        [SerializeField] private PlayerCrouchConfig _crouchConfig;
        [SerializeField] private PlayerCamera _camera;
        [SerializeField] private PlayerAnimator _animator;
        [SerializeField] private bool _lockCursor = true;
        [SerializeField] private float _logInterval = 0.5f;

        private PlayerStateMachine _stateMachine;
        private PlayerContext _context;
        private float _nextLogTime;

        public PlayerStateMachine StateMachine => _stateMachine;

        private void Awake()
        {
            if (_input == null)
            {
                _input = GetComponent<PlayerInputReader>();
            }

            if (_motor == null)
            {
                _motor = GetComponent<CharacterMotor>();
            }

            if (_animator == null)
            {
                _animator = GetComponent<PlayerAnimator>();
            }

            if (_lockCursor)
            {
                ApplyCursorLock();
            }
        }

        private void Start()
        {
            if (_input == null || _motor == null || _motor.Config == null)
            {
                return;
            }

            if (_staminaConfig == null)
            {
                Debug.LogWarning("PlayerController: PlayerStaminaConfig не назначен — спринт и ролл будут недоступны.", this);
            }

            if (_crouchConfig == null)
            {
                Debug.LogWarning("PlayerController: PlayerCrouchConfig не назначен — присед будет работать в режиме Toggle с настройками по умолчанию.", this);
            }

            _context = new PlayerContext(_input, _motor, _motor.Config, _staminaConfig, _crouchConfig, _camera, transform);
            _stateMachine = new PlayerStateMachine(_context);
            _stateMachine.Start();
        }

        private void Update()
        {
            UpdateCursorLock();

            if (_stateMachine == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _context.Stamina.Tick(deltaTime);
            _context.Tick(deltaTime);
            _context.Crouch.Tick(deltaTime);
            if (_camera != null)
            {
                _camera.SetCrouchAmount(_context.Crouch.CrouchAmount);
            }

            if (_input.JumpPressed)
            {
                _context.BufferJump();
            }

            _motor.SetJumpHeld(_input.JumpHeld);
            _motor.Tick(deltaTime);
            _stateMachine.Tick(deltaTime);

            if (_animator != null)
            {
                _animator.Tick(_context, _stateMachine);
            }

            LogState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyCursorLock();
            }
        }

        private void UpdateCursorLock()
        {
            if (!_lockCursor || Cursor.lockState == CursorLockMode.Locked)
            {
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ApplyCursorLock();
            }
        }

        private void ApplyCursorLock()
        {
            if (!_lockCursor)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LogState()
        {
            if (Time.unscaledTime < _nextLogTime)
            {
                return;
            }

            bool active = _input.Move.sqrMagnitude > 0.01f
                || _input.SprintHeld
                || !_motor.IsGrounded
                || _context.IsInvulnerable
                || !_context.CanRoll
                || _context.Stamina.IsExhausted
                || _context.Crouch.WantsCrouch
                || _context.Crouch.IsCrouched;
            if (!active)
            {
                return;
            }

            _nextLogTime = Time.unscaledTime + _logInterval;
            //Debug.Log($"State={_stateMachine.CurrentName} Grounded={_motor.IsGrounded} Coyote={_motor.CoyoteActive} V={_motor.VerticalVelocity:F2} H={_motor.HorizontalSpeed:F2} Inv={_context.IsInvulnerable} RollCD={_context.RollCooldownRemaining:F2} Buf={_context.JumpBufferRemaining:F2} St={_context.Stamina.Current:F1}/{_context.Stamina.Max:F0} Exh={_context.Stamina.IsExhausted} Crouch={_context.Crouch.IsCrouched} WantsCrouch={_context.Crouch.WantsCrouch} CanStand={_context.Crouch.CanStand} FreeH={_context.Crouch.FreeHeight:F2} CH={_context.Crouch.CurrentHeight:F2} Step={_context.Crouch.StepOffset:F2} CrouchAmount={_context.Crouch.CrouchAmount:F2}", this);
        }
    }
}