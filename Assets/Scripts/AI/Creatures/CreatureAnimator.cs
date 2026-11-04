using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Передаёт в Animator существа скорость его горизонтального движения. Не управляет поведением:
    /// только публикует параметр <c>Speed</c> (float, м/с) из <see cref="NavMeshAgent.velocity"/>.
    /// Имя параметра должно совпадать с Animator Controller существа — оно настраивается вручную.
    /// </summary>
    public sealed class CreatureAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        [SerializeField] private Animator _animator;
        [SerializeField] private CreatureController _controller;

        private bool _hasAttackParameter;
        private bool _attackWarningLogged;
        private bool _wasAttacking;

        public Animator Animator => _animator;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_controller == null)
            {
                _controller = GetComponent<CreatureController>();
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _hasAttackParameter = HasParameter(AttackHash);
            }
            else
            {
                Debug.LogWarning("CreatureAnimator: Animator не найден — параметры движения не передаются.", this);
            }
        }

        private void Update()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetFloat(SpeedHash, GetHorizontalSpeed());
            UpdateAttackTrigger();
        }

        /// <summary>
        /// Один раз за замах отправляет триггер <c>Attack</c> (DINO-029). Триггер, а не bool:
        /// клип атаки проигрывается целиком, а повторный триггер ставит только новую атаку
        /// после паузы. Если параметра нет, недостаток анимации не мешает поведению —
        /// предупреждение пишется один раз.
        /// </summary>
        private void UpdateAttackTrigger()
        {
            bool attacking = _controller != null &&
                             _controller.Attack != null &&
                             _controller.Attack.IsAttacking;

            if (attacking && !_wasAttacking)
            {
                if (_hasAttackParameter)
                {
                    _animator.SetTrigger(AttackHash);
                }
                else if (!_attackWarningLogged)
                {
                    _attackWarningLogged = true;
                    Debug.LogWarning(
                        "CreatureAnimator: в Raptor.controller нет параметра Attack — атака без анимации. " +
                        "Добавьте trigger Attack и клип атаки (см. manual-queue.md).",
                        this);
                }
            }

            _wasAttacking = attacking;
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

        /// <summary>Скорость агента по горизонтали; 0, если контроллер или навигатор ещё не готовы.</summary>
        private float GetHorizontalSpeed()
        {
            if (_controller == null)
            {
                return 0f;
            }

            CreatureNavigator navigator = _controller.Navigator;
            if (navigator == null || !navigator.IsValid)
            {
                return 0f;
            }

            Vector3 velocity = navigator.Agent.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }
}
