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

        [SerializeField] private Animator _animator;
        [SerializeField] private CreatureController _controller;

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
