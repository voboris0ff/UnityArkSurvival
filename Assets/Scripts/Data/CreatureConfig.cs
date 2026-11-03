using UnityEngine;

namespace ArkSurvival.Data
{
    [CreateAssetMenu(fileName = "CreatureConfig", menuName = "ArkSurvival/Creature Config")]
    public sealed class CreatureConfig : ScriptableObject
    {
        [Header("Navigation")]
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _chaseSpeed = 6f;
        [SerializeField] private float _turnSpeed = 5f;
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
        [SerializeField] private float _searchDuration = 6f;

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
        public float SearchDuration => _searchDuration;
        public float AttackRange => _attackRange;
        public float AttackAngle => _attackAngle;
        public float AttackCooldown => _attackCooldown;
        public float AttackWindup => _attackWindup;
        public float AttackDamage => _attackDamage;
    }
}
