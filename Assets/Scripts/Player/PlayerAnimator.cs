using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
        private static readonly int CrouchAmountHash = Animator.StringToHash("CrouchAmount");
        private static readonly int RollHash = Animator.StringToHash("Roll");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int ChopHash = Animator.StringToHash("Chop");
        private static readonly int CarryingHash = Animator.StringToHash("IsCarrying");
        private const string CarryingParameterName = "IsCarrying";

        [SerializeField] private Animator _animator;

        private bool _wasRolling;
        private bool _hasDeadParameter;
        private bool _deadWarningLogged;
        private bool _hasChopParameter;
        private bool _chopWarningLogged;
        private bool _hasCarryingParameter;
        private bool _carryingWarningLogged;
        private bool _animatorWarningLogged;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator == null)
            {
                LogAnimatorWarning();
                return;
            }

            _animator.applyRootMotion = false;
            _hasDeadParameter = HasParameter(DeadHash);
            _hasChopParameter = HasParameter(ChopHash);
            _hasCarryingParameter = HasParameter(CarryingHash);
            SetCarrying(false);
        }

        public void Tick(PlayerContext context, PlayerStateMachine machine)
        {
            if (_animator == null || machine == null)
            {
                return;
            }

            bool rolling = machine.Current == machine.Roll;

            float verticalSpeed = context.Motor.IsGrounded ? 0f : context.Motor.VerticalVelocity;

            _animator.SetFloat(SpeedHash, context.Motor.HorizontalSpeed);
            _animator.SetBool(GroundedHash, context.Motor.IsGrounded);
            _animator.SetFloat(VerticalSpeedHash, verticalSpeed);
            _animator.SetBool(SprintingHash, machine.Current == machine.Sprint && !context.Crouch.IsCrouched);
            _animator.SetBool(CrouchingHash, context.Crouch.IsCrouched);
            _animator.SetFloat(CrouchAmountHash, context.Crouch.CrouchAmount);

            if (rolling && !_wasRolling)
            {
                _animator.SetTrigger(RollHash);
            }

            _wasRolling = rolling;
        }

        /// <summary>
        /// Публикует смерть игрока (DINO-028). Параметр <c>Dead</c> необязателен: если его нет
        /// в контроллере, движение просто останавливается, а предупреждение пишется один раз.
        /// </summary>
        public void SetDead(bool dead)
        {
            if (_animator == null)
            {
                return;
            }

            if (!_hasDeadParameter)
            {
                if (!_deadWarningLogged)
                {
                    _deadWarningLogged = true;
                    Debug.LogWarning(
                        "PlayerAnimator: в Player.controller нет параметра Dead — смерть без анимации. " +
                        "Добавьте bool-параметр Dead (см. manual-queue.md).",
                        this);
                }

                return;
            }

            _animator.SetBool(DeadHash, dead);
        }

        /// <summary>
        /// Запускает замах (DINO-038). Триггер <c>Chop</c> необязателен: если параметра нет
        /// в контроллере, замах всё равно начинается, а предупреждение пишется один раз.
        /// </summary>
        public void TriggerChop()
        {
            if (_animator == null)
            {
                return;
            }

            if (!_hasChopParameter)
            {
                if (!_chopWarningLogged)
                {
                    _chopWarningLogged = true;
                    Debug.LogWarning(
                        "PlayerAnimator: в Player.controller нет параметра Chop — замах без анимации. " +
                        "Добавьте trigger-параметр Chop (см. manual-queue.md).",
                        this);
                }

                return;
            }

            _animator.SetTrigger(ChopHash);
        }

        /// <summary>
        /// Публикует состояние переноски поленьев (DINO-058). Параметр <c>IsCarrying</c>
        /// необязателен: если его нет в контроллере, предупреждение пишется один раз.
        /// </summary>
        public void SetCarrying(bool isCarrying)
        {
            if (_animator == null)
            {
                LogAnimatorWarning();
                return;
            }

            if (!_hasCarryingParameter)
            {
                if (!_carryingWarningLogged)
                {
                    _carryingWarningLogged = true;
                    Debug.LogWarning(
                        "PlayerAnimator: в Player.controller нет параметра IsCarrying — переноска без анимации. " +
                        "Добавьте bool-параметр IsCarrying (см. manual-queue.md).",
                        this);
                }

                return;
            }

            _animator.SetBool(CarryingHash, isCarrying);
        }

        private void LogAnimatorWarning()
        {
            if (_animatorWarningLogged)
            {
                return;
            }

            _animatorWarningLogged = true;
            Debug.LogWarning(
                "PlayerAnimator: Animator не найден — анимации отключены, параметр " +
                CarryingParameterName + " не публикуется.",
                this);
        }

        private bool HasParameter(int parameterHash)
        {
            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == parameterHash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
