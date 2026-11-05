using UnityEngine;

namespace ArkSurvival.Data
{
    /// <summary>
    /// Параметры подбора поленьев (DINO-046). Только данные: значения <c>&lt;= 0</c>
    /// заменяются безопасными по умолчанию, чтобы поиск всегда оставался рабочим.
    /// </summary>
    [CreateAssetMenu(fileName = "LogPickupConfig", menuName = "ArkSurvival/Log Pickup Config")]
    public sealed class LogPickupConfig : ScriptableObject
    {
        private const float DefaultPickupRange = 2.5f;
        private const int DefaultLogLayerMask = ~0;
        private const float DefaultPickupLockAfterDrop = 0.75f;

        [Tooltip("Радиус поиска поленьев вокруг игрока, м (временное).")]
        [SerializeField] private float _pickupRange = DefaultPickupRange;

        [Tooltip("Слои поленьев; пустая маска — все слои.")]
        [SerializeField] private LayerMask _logLayerMask = DefaultLogLayerMask;

        [Tooltip("Сколько секунд после принудительного сброса поленья не подбираются, с (временное).")]
        [SerializeField] private float _pickupLockAfterDrop = DefaultPickupLockAfterDrop;

        /// <summary>Радиус поиска; не больше нуля — значение по умолчанию.</summary>
        public float PickupRange => _pickupRange > 0f ? _pickupRange : DefaultPickupRange;

        /// <summary>Маска слоёв поленьев; пустая маска — все слои.</summary>
        public LayerMask LogLayerMask =>
            _logLayerMask.value == 0 ? DefaultLogLayerMask : _logLayerMask;

        /// <summary>
        /// Блокировка подбора после принудительного сброса, с; не больше нуля — значение
        /// по умолчанию.
        /// </summary>
        public float PickupLockAfterDrop =>
            _pickupLockAfterDrop > 0f ? _pickupLockAfterDrop : DefaultPickupLockAfterDrop;

        private void OnValidate()
        {
            if (_pickupRange <= 0f)
            {
                _pickupRange = DefaultPickupRange;
            }

            if (_logLayerMask.value == 0)
            {
                _logLayerMask = DefaultLogLayerMask;
            }

            if (_pickupLockAfterDrop <= 0f)
            {
                _pickupLockAfterDrop = DefaultPickupLockAfterDrop;
            }
        }
    }
}
