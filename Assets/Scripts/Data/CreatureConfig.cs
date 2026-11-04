using UnityEngine;

namespace ArkSurvival.Data
{
    [CreateAssetMenu(fileName = "CreatureConfig", menuName = "ArkSurvival/Creature Config")]
    public sealed class CreatureConfig : ScriptableObject
    {
        [Header("Navigation")]
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _chaseSpeed = 6f;
        [Tooltip("Максимальная угловая скорость разворота существа, градусы/с. <= 0 — поворот без ограничения (силами NavMeshAgent).")]
        [SerializeField] private float _turnSpeed = 180f;
        [SerializeField] private float _arrivalDistance = 0.5f;
        [SerializeField] private float _destinationUpdateInterval = 0.15f;

        [Header("Perception")]
        [SerializeField] private float _perceptionInterval = 0.2f;
        [SerializeField] private float _sightDistance = 20f;
        [SerializeField] private float _sightAngle = 110f;
        [SerializeField] private float _eyeHeight = 1.5f;
        [SerializeField] private LayerMask _obstacleLayerMask = 1;
        [SerializeField] private float _hearingRange = 12f;

        [Header("Memory")]
        [SerializeField] private float _memoryDuration = 5f;
        [SerializeField] private float _investigateDuration = 8f;
        [SerializeField] private float _searchDuration = 6f;
        [Tooltip("Радиус случайных точек поиска вокруг последней известной позиции, м (временное).")]
        [SerializeField] private float _searchRadius = 8f;
        [Tooltip("Пауза на точке поиска перед переходом к следующей, с (временное).")]
        [SerializeField] private float _searchStopDuration = 1f;

        [Header("Patrol")]
        [Tooltip("Радиус случайных точек патруля вокруг домашней точки, м (временное).")]
        [SerializeField] private float _patrolRadius = 10f;
        [Tooltip("Скорость перемещения по патрулю, м/с (временное).")]
        [SerializeField] private float _patrolSpeed = 2f;
        [Tooltip("Шанс остановиться на точке патруля, 0..1 (временное).")]
        [SerializeField, Range(0f, 1f)] private float _patrolWaitChance = 0.5f;
        [Tooltip("Минимальная пауза на точке патруля, с (временное).")]
        [SerializeField] private float _patrolWaitMin = 1f;
        [Tooltip("Максимальная пауза на точке патруля, с (временное).")]
        [SerializeField] private float _patrolWaitMax = 3f;

        [Header("Attack")]
        [SerializeField] private float _attackRange = 2.5f;
        [SerializeField] private float _attackAngle = 90f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private float _attackWindup = 0.4f;
        [SerializeField] private float _attackDamage = 10f;

        public float MoveSpeed => _moveSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float TurnSpeed => _turnSpeed;
        public float ArrivalDistance => _arrivalDistance;
        public float DestinationUpdateInterval => _destinationUpdateInterval;
        public float PerceptionInterval => _perceptionInterval;
        public float SightDistance => _sightDistance;
        public float SightAngle => _sightAngle;
        public float EyeHeight => _eyeHeight;
        public LayerMask ObstacleLayerMask => _obstacleLayerMask;
        public float HearingRange => _hearingRange;
        public float MemoryDuration => _memoryDuration;
        public float InvestigateDuration => _investigateDuration;
        public float SearchDuration => _searchDuration;
        public float SearchRadius => _searchRadius;
        public float SearchStopDuration => _searchStopDuration;
        public float PatrolRadius => _patrolRadius;
        public float PatrolSpeed => _patrolSpeed;
        public float PatrolWaitChance => _patrolWaitChance;
        public float PatrolWaitMin => _patrolWaitMin;
        public float PatrolWaitMax => _patrolWaitMax;
        public float AttackRange => _attackRange;
        public float AttackAngle => _attackAngle;
        public float AttackCooldown => _attackCooldown;
        public float AttackWindup => _attackWindup;
        public float AttackDamage => _attackDamage;
    }
}
