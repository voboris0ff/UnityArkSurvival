using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private PlayerCamera _camera;
        [SerializeField] private bool _lockCursor = true;
        [SerializeField] private float _logInterval = 0.5f;

        private PlayerStateMachine _stateMachine;
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

            if (_lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Start()
        {
            if (_input == null || _motor == null || _motor.Config == null)
            {
                return;
            }

            PlayerContext context = new PlayerContext(_input, _motor, _motor.Config, _camera, transform);
            _stateMachine = new PlayerStateMachine(context);
            _stateMachine.Start();
        }

        private void Update()
        {
            if (_stateMachine == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _motor.SetJumpHeld(_input.JumpHeld);
            _motor.Tick(deltaTime);
            _stateMachine.Tick(deltaTime);
            LogState();
        }

        private void LogState()
        {
            if (Time.unscaledTime < _nextLogTime)
            {
                return;
            }

            bool active = _input.Move.sqrMagnitude > 0.01f || _input.SprintHeld || !_motor.IsGrounded;
            if (!active)
            {
                return;
            }

            _nextLogTime = Time.unscaledTime + _logInterval;
            Debug.Log(
                $"State={_stateMachine.CurrentName} Grounded={_motor.IsGrounded} Coyote={_motor.CoyoteActive} V={_motor.VerticalVelocity:F2} H={_motor.HorizontalSpeed:F2}",
                this);
        }
    }
}