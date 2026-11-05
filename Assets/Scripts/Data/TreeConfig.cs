using UnityEngine;

namespace ArkSurvival.Data
{
    [CreateAssetMenu(fileName = "TreeConfig", menuName = "ArkSurvival/Tree Config")]
    public sealed class TreeConfig : ScriptableObject
    {
        [Tooltip("Прочность дерева в единицах урона до падения (временное).")]
        [SerializeField] private float _maxHealth = 100f;
        [Tooltip("Сколько поленьев выпадает при падении дерева (временное).")]
        [SerializeField] private int _logsCount = 3;
        [Tooltip("Начальный импульс разлёта поленьев, м/с (временное).")]
        [SerializeField] private float _logSpawnImpulse = 2f;
        [Tooltip("Радиус разброса точек появления поленьев, м (временное).")]
        [SerializeField] private float _logSpawnRadius = 0.5f;
        [Tooltip("Высота появления поленьев над основанием дерева, м (временное).")]
        [SerializeField] private float _logSpawnHeight = 0.5f;

        public float MaxHealth => _maxHealth;
        public int LogsCount => _logsCount;
        public float LogSpawnImpulse => _logSpawnImpulse;
        public float LogSpawnRadius => _logSpawnRadius;
        public float LogSpawnHeight => _logSpawnHeight;
    }
}
