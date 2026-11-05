using UnityEngine;

namespace ArkSurvival.Player
{
    /// <summary>
    /// Приёмник Animation Event клипа замаха (DINO-038). Компонент ставится на тот же GameObject,
    /// что и Animator игрока (обычно модель), потому что Unity отправляет события только
    /// компонентам этого объекта. Событие вызывает <see cref="PlayerChopInteractor.NotifyAnimationHit"/>:
    /// удар наносится в кадре клипа, а таймер <c>ChopHitTime</c> остаётся запасным путём.
    /// </summary>
    public sealed class PlayerChopAnimationEvent : MonoBehaviour
    {
        [SerializeField] private PlayerChopInteractor _interactor;

        private void Awake()
        {
            if (_interactor == null)
            {
                _interactor = GetComponentInParent<PlayerChopInteractor>();
            }

            if (_interactor == null)
            {
                Debug.LogWarning(
                    "PlayerChopAnimationEvent: PlayerChopInteractor не найден — Animation Event удара ничего не сделает.",
                    this);
            }
        }

        /// <summary>
        /// Вызывается Animation Event в кадре удара. Имя метода совпадает с настраиваемым
        /// вручную событием клипа (см. manual-queue.md); аргументов у события нет.
        /// </summary>
        public void OnChopHit()
        {
            if (_interactor == null)
            {
                return;
            }

            _interactor.NotifyAnimationHit();
        }
    }
}
