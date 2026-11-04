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

        /// <summary>Минимальная горизонтальная скорость, при которой существо ещё доворачивается по движению, м/с.</summary>
        private const float MinRotationSpeed = 0.1f;

        /// <summary>
        /// Если направление движения расходится с текущим взглядом сильнее этого угла,
        /// существо сначала разворачивается на месте, а не «скользит» вбок (временное значение).
        /// </summary>
        private const float MoveAlignAngle = 60f;

        private readonly NavMeshAgent _agent;
        private readonly NavMeshPath _pathProbe = new NavMeshPath();
        private float _arrivalDistance;
        private float _defaultSpeed;
        private float _turnSpeedDegrees;
        private float _requestedSpeed;
        private bool _movementBlocked;
        private bool _stopRequested;

        public CreatureNavigator(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public bool IsValid => _agent != null;

        public NavMeshAgent Agent => _agent;

        public bool IsReady => _agent != null && _agent.enabled && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

        public bool HasPath => IsReady && !_agent.pathPending && _agent.hasPath && _agent.pathStatus != NavMeshPathStatus.PathInvalid;

        public bool IsMoving => IsReady && _agent.hasPath && !_stopRequested;

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
        /// <param name="turnSpeedDegrees">Максимальная угловая скорость разворота, градусы/с; <c>&lt;= 0</c> — поворот не ограничивается.</param>
        public void Initialize(float arrivalDistance, float defaultSpeed, float turnSpeedDegrees = 0f)
        {
            _arrivalDistance = Mathf.Max(arrivalDistance, MinPathTolerance);
            _defaultSpeed = Mathf.Max(defaultSpeed, 0f);
            _turnSpeedDegrees = Mathf.Max(turnSpeedDegrees, 0f);
            _requestedSpeed = _defaultSpeed;
            _movementBlocked = false;
            _stopRequested = false;

            if (_agent == null)
            {
                return;
            }

            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.isStopped = false;
            _agent.speed = _defaultSpeed;
            _agent.stoppingDistance = Mathf.Max(_agent.stoppingDistance, _arrivalDistance);

            // Поворотом управляет TickRotation по CreatureConfig.TurnSpeed, иначе NavMeshAgent
            // разворачивает существо мгновенно и конфиг на поворот не влияет.
            // TurnSpeed <= 0 оставляет поворот агенту (без ограничения).
            _agent.updateRotation = _turnSpeedDegrees <= 0f;
        }

        /// <summary>
        /// Возвращает true, если агенту уже поставлен путь и он ещё не пройден.
        /// Пока путь считается, у агента <c>hasPath == false</c>, а <c>remainingDistance</c> равен 0,
        /// поэтому сначала проверяется <c>pathPending</c>, а прибытие определяется по дистанции до destination.
        /// Частичный путь (цель недостижима целиком) считается неактивным: состояния по этому признаку
        /// отказываются от недостижимой точки, а не идут к её краю (DINO-031).
        /// </summary>
        public bool IsPathActive
        {
            get
            {
                if (!IsReady || _stopRequested)
                {
                    return false;
                }

                if (_agent.pathPending)
                {
                    return true;
                }

                if (!_agent.hasPath ||
                    _agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                    _agent.pathStatus == NavMeshPathStatus.PathPartial)
                {
                    return false;
                }

                return !HasArrived;
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
            _stopRequested = false;
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
            _requestedSpeed = Mathf.Max(0f, speed);

            if (_agent == null || _movementBlocked)
            {
                // Пока идёт разворот, скорость восстановит TickRotation (ApplySpeed).
                return;
            }

            _agent.speed = _requestedSpeed;
        }

        public void ResetToSpeed()
        {
            SetSpeed(_defaultSpeed);
        }

        /// <summary>
        /// Применяет запрошенную скорость, но пока существо не развернулось к направлению движения,
        /// ставит агента на паузу — иначе он уезжает боком/спиной (NavMeshAgent не умеет ездить только вперёд).
        /// Возвращает true, если движение сейчас заблокировано разворотом.
        /// </summary>
        private bool ApplySpeed()
        {
            bool shouldBlock = _turnSpeedDegrees > 0f && !IsAlignedToMovement();
            _movementBlocked = shouldBlock;

            // isStopped выставляется каждый вызов: SetDestination мог сбросить его, а пауза на разворот
            // должна сохраняться, пока направление не совпадёт с путём.
            _agent.isStopped = shouldBlock;
            _agent.speed = shouldBlock ? 0f : _requestedSpeed;
            return shouldBlock;
        }

        /// <summary>
        /// Направление движения совпадает с текущим взглядом в пределах <see cref="MoveAlignAngle"/>.
        /// Направление берётся из desiredVelocity, пока агент едет, иначе — из следующей точки пути.
        /// </summary>
        private bool IsAlignedToMovement()
        {
            Vector3 direction = GetMoveDirection();
            if (direction.sqrMagnitude <= MinPathTolerance * MinPathTolerance)
            {
                return true;
            }

            Vector3 forward = _agent.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= MinPathTolerance * MinPathTolerance)
            {
                return true;
            }

            return Vector3.Angle(forward, direction) <= MoveAlignAngle;
        }

        private Vector3 GetMoveDirection()
        {
            Vector3 desiredVelocity = _agent.desiredVelocity;
            desiredVelocity.y = 0f;

            return desiredVelocity.sqrMagnitude > MinRotationSpeed * MinRotationSpeed
                ? desiredVelocity
                : GetPathDirection();
        }

        public void Stop()
        {
            if (!IsReady)
            {
                return;
            }

            _stopRequested = true;
            _movementBlocked = false;
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        /// <summary>
        /// Поворачивать существо в сторону движения силами NavMeshAgent. С 35-й задачей поворот
        /// выполняет <see cref="TickRotation"/>, поэтому по умолчанию агент его не делает.
        /// </summary>
        public void SetUpdateRotation(bool enabled)
        {
            if (IsValid)
            {
                _agent.updateRotation = enabled;
            }
        }

        /// <summary>
        /// Плавно доворачивает существо к направлению движения с ограничением угловой скорости
        /// <c>TurnSpeed</c> (градусы/с). Вызывается каждый кадр из <c>CreatureController.Update</c>.
        /// Пока существо стоит, направление берётся из следующей точки пути — так оно
        /// успевает развернуться перед началом движения.
        /// </summary>
        public void TickRotation(float deltaTime)
        {
            if (!IsReady || deltaTime <= 0f || _turnSpeedDegrees <= 0f)
            {
                // Без лимита поворот оставлен NavMeshAgent (см. Initialize).
                return;
            }

            Vector3 direction = GetMoveDirection();

            if (direction.sqrMagnitude > MinPathTolerance * MinPathTolerance)
            {
                Vector3 forward = _agent.transform.forward;
                forward.y = 0f;

                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                if (forward.sqrMagnitude > MinPathTolerance * MinPathTolerance)
                {
                    targetRotation = Quaternion.RotateTowards(
                        Quaternion.LookRotation(forward.normalized, Vector3.up),
                        targetRotation,
                        _turnSpeedDegrees * deltaTime);
                }

                _agent.transform.rotation = targetRotation;
            }

            // Скорость применяется здесь же: пока существо не развернулось, оно стоит на месте,
            // поэтому забежать ему за спину не спасает — оно сначала разворачивается, потом едет.
            ApplySpeed();
        }

        /// <summary>
        /// Направление на следующую точку маршрута (без вертикали); ноль, если маршрута нет.
        /// <c>steeringTarget</c> вместо <c>path.corners</c> — не создаёт массив каждый кадр.
        /// </summary>
        private Vector3 GetPathDirection()
        {
            if (_agent.pathPending || !_agent.hasPath)
            {
                return Vector3.zero;
            }

            Vector3 direction = _agent.steeringTarget - _agent.transform.position;
            direction.y = 0f;
            return direction;
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
