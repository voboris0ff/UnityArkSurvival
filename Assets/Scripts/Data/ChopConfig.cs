using UnityEngine;

namespace ArkSurvival.Data
{
    /// <summary>
    /// Параметры рубки деревьев игроком (DINO-035). Только данные: значения <c>&lt;= 0</c>
    /// заменяются безопасными по умолчанию, чтобы рубка оставалась работоспособной.
    /// </summary>
    [CreateAssetMenu(fileName = "ChopConfig", menuName = "ArkSurvival/Chop Config")]
    public sealed class ChopConfig : ScriptableObject
    {
        private const float DefaultChopDamage = 10f;
        private const float DefaultChopRange = 2f;
        private const float DefaultChopAngle = 90f;
        private const float DefaultChopCooldown = 0.8f;
        private const float DefaultChopSwingDuration = 0.6f;
        private const float DefaultChopHitTime = 0.3f;
        private const int DefaultTreeLayerMask = ~0;

        [Tooltip("Урон одного удара по дереву (временное).")]
        [SerializeField] private float _chopDamage = DefaultChopDamage;

        [Tooltip("Дистанция рубки от игрока, м (временное).")]
        [SerializeField] private float _chopRange = DefaultChopRange;

        [Tooltip("Полный угол сектора рубки, градусы; 90° = ±45° от направления вперёд (временное).")]
        [SerializeField] private float _chopAngle = DefaultChopAngle;

        [Tooltip("Пауза между ударами, с (временное).")]
        [SerializeField] private float _chopCooldown = DefaultChopCooldown;

        [Tooltip("Длительность замаха от начала до конца, с (временное).")]
        [SerializeField] private float _chopSwingDuration = DefaultChopSwingDuration;

        [Tooltip("Момент удара внутри замаха, с (временное).")]
        [SerializeField] private float _chopHitTime = DefaultChopHitTime;

        [Tooltip("Слои деревьев; пустая маска — все слои.")]
        [SerializeField] private LayerMask _treeLayerMask = DefaultTreeLayerMask;

        /// <summary>Урон одного удара; не больше нуля — значение по умолчанию.</summary>
        public float ChopDamage => _chopDamage > 0f ? _chopDamage : DefaultChopDamage;

        /// <summary>Дистанция рубки; не больше нуля — значение по умолчанию.</summary>
        public float ChopRange => _chopRange > 0f ? _chopRange : DefaultChopRange;

        /// <summary>Полный угол сектора рубки; не больше нуля — значение по умолчанию.</summary>
        public float ChopAngle => _chopAngle > 0f ? _chopAngle : DefaultChopAngle;

        /// <summary>Пауза между ударами; не больше нуля — значение по умолчанию.</summary>
        public float ChopCooldown => _chopCooldown > 0f ? _chopCooldown : DefaultChopCooldown;

        /// <summary>Длительность замаха; не больше нуля — значение по умолчанию.</summary>
        public float ChopSwingDuration =>
            _chopSwingDuration > 0f ? _chopSwingDuration : DefaultChopSwingDuration;

        /// <summary>Момент удара внутри замаха; не больше нуля — значение по умолчанию.</summary>
        public float ChopHitTime => Mathf.Clamp(
            _chopHitTime > 0f ? _chopHitTime : DefaultChopHitTime,
            0f,
            ChopSwingDuration);

        /// <summary>Маска слоёв деревьев; пустая маска — все слои.</summary>
        public LayerMask TreeLayerMask => _treeLayerMask.value == 0 ? DefaultTreeLayerMask : _treeLayerMask;

        private void OnValidate()
        {
            if (_chopDamage <= 0f)
            {
                _chopDamage = DefaultChopDamage;
            }

            if (_chopRange <= 0f)
            {
                _chopRange = DefaultChopRange;
            }

            if (_chopAngle <= 0f)
            {
                _chopAngle = DefaultChopAngle;
            }

            if (_chopCooldown <= 0f)
            {
                _chopCooldown = DefaultChopCooldown;
            }

            if (_chopSwingDuration <= 0f)
            {
                _chopSwingDuration = DefaultChopSwingDuration;
            }

            if (_chopHitTime <= 0f)
            {
                _chopHitTime = DefaultChopHitTime;
            }

            _chopHitTime = Mathf.Min(_chopHitTime, _chopSwingDuration);

            if (_treeLayerMask.value == 0)
            {
                _treeLayerMask = DefaultTreeLayerMask;
            }
        }
    }
}
