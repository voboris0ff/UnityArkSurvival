using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        private static readonly int RollHash = Animator.StringToHash("Roll");

        [SerializeField] private Animator _animator;

        private bool _wasRolling;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
        }

        public void Tick(PlayerContext context, PlayerStateMachine machine)
        {
            if (_animator == null || machine == null)
            {
                return;
            }

            bool rolling = machine.Current == machine.Roll;

            _animator.SetFloat(SpeedHash, context.Motor.HorizontalSpeed);
            _animator.SetBool(GroundedHash, context.Motor.IsGrounded);
            _animator.SetFloat(VerticalSpeedHash, context.Motor.VerticalVelocity);
            _animator.SetBool(SprintingHash, machine.Current == machine.Sprint);

            if (rolling && !_wasRolling)
            {
                _animator.SetTrigger(RollHash);
            }

            _wasRolling = rolling;
        }
    }
}