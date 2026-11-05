using UnityEngine;

namespace ArkSurvival.Data
{
    /// <summary>
    /// Параметры переноски поленьев (DINO-045). Только данные: значения <c>&lt;= 0</c>
    /// заменяются безопасными по умолчанию, чтобы лимит всегда был рабочим.
    /// </summary>
    [CreateAssetMenu(fileName = "LogCarryConfig", menuName = "ArkSurvival/Log Carry Config")]
    public sealed class LogCarryConfig : ScriptableObject
    {
        private const int DefaultMaxCarriedLogs = 3;

        [Tooltip("Сколько поленьев игрок может нести одновременно (временное).")]
        [SerializeField] private int _maxCarriedLogs = DefaultMaxCarriedLogs;

        /// <summary>Лимит переносимых поленьев; не больше нуля — значение по умолчанию.</summary>
        public int MaxCarriedLogs =>
            _maxCarriedLogs > 0 ? _maxCarriedLogs : DefaultMaxCarriedLogs;

        private void OnValidate()
        {
            if (_maxCarriedLogs <= 0)
            {
                _maxCarriedLogs = DefaultMaxCarriedLogs;
            }
        }
    }
}
