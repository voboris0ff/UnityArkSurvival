using System;
using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.World
{
    /// <summary>
    /// Дерево, которое можно срубить (DINO-034): хранит здоровье из <see cref="TreeConfig"/>,
    /// принимает урон через <see cref="TakeChopDamage"/> и ровно один раз публикует
    /// <see cref="OnFelled"/>. Спавн поленьев и взаимодействие с игроком сюда не входят.
    /// </summary>
    public sealed class ChoppableTree : MonoBehaviour
    {
        /// <summary>Здоровье при отсутствии конфига (временное): одного удара хватает на валку.</summary>
        private const float FallbackMaxHealth = 1f;

        [Tooltip("Конфигурация дерева. Пусто — используется минимальное здоровье и пишется предупреждение.")]
        [SerializeField] private TreeConfig _config;

        private float _health;
        private bool _isFelled;

        /// <summary>
        /// Урон принят: <c>(дерево, фактически снятое здоровье, позиция источника)</c>.
        /// Приходит и на смертельный удар — раньше, чем <see cref="OnFelled"/>.
        /// </summary>
        public event Action<ChoppableTree, float, Vector3> OnDamaged;

        /// <summary>
        /// Дерево срублено: <c>(дерево, позиция источника)</c>. Публикуется один раз за жизнь дерева.
        /// </summary>
        public event Action<ChoppableTree, Vector3> OnFelled;

        /// <summary>Текущее здоровье; 0 после валки.</summary>
        public float Health => _health;

        /// <summary>Максимальное здоровье из конфига.</summary>
        public float MaxHealth { get; private set; }

        /// <summary>Дерево срублено: урон больше не принимается, <see cref="OnFelled"/> уже был.</summary>
        public bool IsFelled => _isFelled;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogWarning(
                    "ChoppableTree: конфиг дерева не назначен — используется минимальное здоровье " +
                    $"{FallbackMaxHealth}. Назначьте TreeConfig.",
                    this);
                MaxHealth = FallbackMaxHealth;
            }
            else
            {
                MaxHealth = Mathf.Max(0f, _config.MaxHealth);
            }

            _health = MaxHealth;
        }

        /// <summary>
        /// Наносит урон рубке. Урон после валки и неположительный урон игнорируются,
        /// здоровье не уходит ниже нуля. Смертельный урон публикует <see cref="OnDamaged"/>,
        /// затем один раз <see cref="OnFelled"/>.
        /// </summary>
        public void TakeChopDamage(float amount, Vector3 source)
        {
            if (_isFelled || !(amount > 0f))
            {
                return;
            }

            float healthBefore = _health;
            _health = Mathf.Max(0f, _health - amount);
            OnDamaged?.Invoke(this, healthBefore - _health, source);

            if (_health > 0f)
            {
                return;
            }

            _isFelled = true;
            OnFelled?.Invoke(this, source);
        }
    }
}
