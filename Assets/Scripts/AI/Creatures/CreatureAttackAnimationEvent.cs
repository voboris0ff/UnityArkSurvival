using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Приёмник Animation Event клипа атаки (DINO-029). Компонент ставится на тот же GameObject,
    /// что и Animator существа (обычно модель), потому что Unity отправляет события только
    /// компонентам этого объекта. Событие вызывает <see cref="CreatureAttack.NotifyAnimationHit"/>:
    /// попадание проверяется в кадре клипа, а таймер <c>AttackHitTime</c> остаётся запасным путём.
    /// </summary>
    public sealed class CreatureAttackAnimationEvent : MonoBehaviour
    {
        [SerializeField] private CreatureController _controller;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponentInParent<CreatureController>();
            }

            if (_controller == null)
            {
                Debug.LogWarning(
                    "CreatureAttackAnimationEvent: CreatureController не найден — Animation Event атаки ничего не сделает.",
                    this);
            }
        }

        /// <summary>
        /// Вызывается Animation Event в кадре удара. Имя метода совпадает с настраиваемым
        /// вручную событием клипа (см. manual-queue.md); аргументов у события нет.
        /// </summary>
        public void OnAttackHit()
        {
            CreatureAttack attack = _controller != null ? _controller.Attack : null;
            if (attack == null)
            {
                return;
            }

            attack.NotifyAnimationHit();
        }
    }
}
