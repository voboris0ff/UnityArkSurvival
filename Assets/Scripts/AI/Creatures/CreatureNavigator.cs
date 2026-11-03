using UnityEngine;
using UnityEngine.AI;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Адаптер над <see cref="NavMeshAgent"/>. Исполняет навигацию по заданной цели,
    /// но не принимает решений: куда идти, какую скорость выбирать, когда останавливаться.
    /// </summary>
    public sealed class CreatureNavigator
    {
        private const float MinPathTolerance = 0.01f;

        private readonly NavMeshAgent _agent;
        private readonly NavMeshPath _pathProbe = new NavMeshPath();
        private float _arrivalDistance;
        private float _defaultSpeed;

        public CreatureNavigator(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public bool IsValid => _agent != null;

        public NavMeshAgent Agent => _agent;

        public bool IsReady => _agent != null && _agent.enabled && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

        public bool HasPath => IsReady && !_agent.pathPending && _agent.hasPath && _agent.pathStatus != NavMeshPathStatus.PathInvalid;

        public bool IsMoving => IsReady && _agent.hasPath && !_agent.isStopped;

        public bool HasArrived
        {
            get
            {
                if (!IsReady)
                {
                    return false;
                }

                Vector3 remaining = _agent.destination - _agent.transform.position;
                remaining.y = 0f;
                return remaining.sqrMagnitude <= _arrivalDistance * _arrivalDistance;
            }
        }

        public Vector3 Position => _agent != null ? _agent.transform.position : Vector3.zero;

        public float RemainingDistance => IsReady && !_agent.pathPending ? _agent.remainingDistance : 0f;

        /// <param name="arrivalDistance">Дистанция по горизонтали до destination, при которой считается, что точка достигнута.</param>
        /// <param name="defaultSpeed">Скорость по умолчанию, применяемая в <see cref="ResetToSpeed"/>.</param>
        public void Initialize(float arrivalDistance, float defaultSpeed)
        {
            _arrivalDistance = Mathf.Max(arrivalDistance, MinPathTolerance);
            _defaultSpeed = Mathf.Max(defaultSpeed, 0f);

            if (_agent == null)
            {
                return;
            }

            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.isStopped = false;
            _agent.speed = _defaultSpeed;
            _agent.stoppingDistance = Mathf.Max(_agent.stoppingDistance, _arrivalDistance);
        }

        /// <summary>
        /// Возвращает true, если агенту уже поставлен путь и он ещё не пройден.
        /// Пока путь считается, у агента <c>hasPath == false</c>, а <c>remainingDistance</c> равен 0,
        /// поэтому сначала проверяется <c>pathPending</c>, а прибытие определяется по дистанции до destination.
        /// </summary>
        public bool IsPathActive
        {
            get
            {
                if (!IsReady || _agent.isStopped)
                {
                    return false;
                }

                if (_agent.pathPending)
                {
                    return true;
                }

                if (!_agent.hasPath || _agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    return false;
                }

                if (_agent.pathStatus == NavMeshPathStatus.PathComplete)
                {
                    return !HasArrived;
                }

                return _agent.remainingDistance > _arrivalDistance;
            }
        }

        /// <summary>
        /// Проверяет, что к точке можно построить путь, не меняя текущий маршрут агента.
        /// </summary>
        public bool CanReach(Vector3 position, float sampleRadius = 0f)
        {
            if (!IsReady || !TrySnapToNavMesh(position, sampleRadius, out Vector3 target))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(_agent.transform.position, target, _agent.areaMask, _pathProbe) ||
                _pathProbe.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Пытается назначить новую точку. Возвращает false, если агент не готов
        /// или ближайшая точка поверхности не оказалась достаточно близко к запрошенной.
        /// </summary>
        /// <param name="allowPartialPath">Если false, частичный путь (до ближайшей достижимой точки) отклоняется.</param>
        /// <param name="sampleRadius">Радиус поиска ближайшей точки NavMesh; 0 — используется дистанция прибытия.</param>
        public bool TrySetDestination(Vector3 position, bool allowPartialPath = false, float sampleRadius = 0f)
        {
            if (!IsReady || !TrySnapToNavMesh(position, sampleRadius, out Vector3 target))
            {
                return false;
            }

            _agent.ResetPath();
            _agent.isStopped = false;
            if (!_agent.SetDestination(target))
            {
                return false;
            }

            // Пока путь считается (pathPending), pathStatus ещё прежний, поэтому проверка откладывается
            // до Tick состояния: частичный путь отклонит IsPathActive.
            if (!allowPartialPath && !_agent.pathPending && _agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                _agent.ResetPath();
                return false;
            }

            return true;
        }

        public void SetSpeed(float speed)
        {
            if (_agent == null)
            {
                return;
            }

            _agent.speed = Mathf.Max(0f, speed);
        }

        public void ResetToSpeed()
        {
            SetSpeed(_defaultSpeed);
        }

        public void Stop()
        {
            if (!IsReady)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        /// <summary>Поворачивать существо в сторону движения силами NavMeshAgent.</summary>
        public void SetUpdateRotation(bool enabled)
        {
            if (IsValid)
            {
                _agent.updateRotation = enabled;
            }
        }

        private bool TrySnapToNavMesh(Vector3 position, float sampleRadius, out Vector3 result)
        {
            result = position;
            if (_agent == null)
            {
                return false;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID,
                areaMask = _agent.areaMask
            };

            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, Mathf.Max(sampleRadius, _arrivalDistance), filter))
            {
                return false;
            }

            result = hit.position;
            return true;
        }
    }
}
