using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Патруль: существо ходит к случайным достижимым точкам вокруг текущей домашней точки.
    /// Точку выбирает <see cref="CreaturePatrolPointSelector"/>; по прибытии с шансом
    /// <c>PatrolWaitChance</c> стоит случайное время и идёт к следующей. Если подходящей точки нет —
    /// короткая пауза и повтор, а не попытка каждый кадр. При смене домашней точки текущий маршрут
    /// сбрасывается и новые точки выбираются вокруг новой. Переходы по зрению выполняет только
    /// <see cref="CreatureStateMachine.ApplyPerception"/>.
    /// </summary>
    public sealed class PatrolState : CreatureState
    {
        /// <summary>Радиус привязки точки к NavMesh, м.</summary>
        private const float PointSampleRadius = 2f;

        /// <summary>Смещение домашней точки, при котором маршрут считается устаревшим, м (временное).</summary>
        private const float HomeChangeEpsilon = 0.05f;

        /// <summary>Пауза перед повторным выбором точки, если подходящей нет, с (временное).</summary>
        private const float RetryDelay = 0.5f;

        private const float DefaultSpeed = 2f;
        private const float DefaultWaitChance = 0.5f;
        private const float DefaultWaitMin = 1f;
        private const float DefaultWaitMax = 3f;

        private Vector3 _homeAtSelection;
        private float _waitRemaining;
        private float _retryRemaining;
        private bool _waiting;
        private bool _destinationAssigned;

        public PatrolState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            _waiting = false;
            _waitRemaining = 0f;
            _retryRemaining = 0f;
            _destinationAssigned = false;
            _homeAtSelection = Machine.Context.HomePosition;

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (!navigator.IsReady)
            {
                Debug.LogWarning("PatrolState: NavMeshAgent не готов — патруль на паузе.", navigator.Agent);
                _retryRemaining = RetryDelay;
                return;
            }

            navigator.SetSpeed(GetSpeed());
            Debug.Log(
                $"PatrolState: патруль вокруг {_homeAtSelection} радиусом {GetRadius():F1} м, " +
                $"скорость {GetSpeed():F1} м/с.",
                navigator.Agent);

            PickNextPoint();
        }

        public override void Exit()
        {
            _waiting = false;
            _destinationAssigned = false;
            _waitRemaining = 0f;
            _retryRemaining = 0f;
        }

        public override void Tick(float deltaTime)
        {
            // Восприятие могло переключить состояние в Chase/Investigate — маршрут не трогаем.
            if (Machine.Current != this)
            {
                return;
            }

            float delta = Mathf.Max(0f, deltaTime);
            CreatureContext context = Machine.Context;
            CreatureNavigator navigator = context.Navigator;

            // Агент стал неготовым (выключен, снят с NavMesh) — патрулировать некуда.
            // Без этого состояние молча зависало бы: Выбор точки требует IsReady.
            if (!navigator.IsReady)
            {
                Debug.LogWarning("PatrolState: NavMeshAgent не готов — выход в Idle.", navigator.Agent);
                Machine.ChangeState(Machine.Idle);
                return;
            }

            // Домашняя точка перенесена (например, неудачный поиск из DINO-016) — старый маршрут
            // больше не относится к патрулю, точку нужно выбирать вокруг новой.
            if (IsHomeChanged(context))
            {
                _homeAtSelection = context.HomePosition;
                _waiting = false;
                _waitRemaining = 0f;
                _destinationAssigned = false;
                if (navigator.IsReady)
                {
                    navigator.Stop();
                }

                Debug.Log($"PatrolState: домашняя точка изменилась — маршрут вокруг {_homeAtSelection}.", navigator.Agent);
                PickNextPoint();
                return;
            }

            if (_waiting)
            {
                _waitRemaining -= delta;
                if (_waitRemaining <= 0f)
                {
                    _waiting = false;
                    PickNextPoint();
                }

                return;
            }

            if (!_destinationAssigned)
            {
                _retryRemaining -= delta;
                if (_retryRemaining <= 0f && navigator.IsReady)
                {
                    PickNextPoint();
                }

                return;
            }

            if (navigator.HasArrived)
            {
                navigator.Stop();
                _destinationAssigned = false;

                if (Random.value < GetWaitChance())
                {
                    _waiting = true;
                    _waitRemaining = GetWaitDuration();
                    return;
                }

                PickNextPoint();
                return;
            }

            // Путь не построился или пропал — выбираем другую точку, но не чаще RetryDelay.
            if (!navigator.IsPathActive)
            {
                _destinationAssigned = false;
                _retryRemaining = RetryDelay;
            }
        }

        private void PickNextPoint()
        {
            CreatureContext context = Machine.Context;
            CreatureNavigator navigator = context.Navigator;

            if (navigator.IsReady && context.PatrolPoints != null &&
                context.PatrolPoints.TrySelectPoint(out Vector3 point) &&
                navigator.TrySetDestination(point, false, PointSampleRadius))
            {
                navigator.SetSpeed(GetSpeed());
                _destinationAssigned = true;
                _retryRemaining = 0f;
                return;
            }

            _destinationAssigned = false;
            _retryRemaining = RetryDelay;

            if (navigator.IsReady)
            {
                navigator.Stop();
            }
        }

        private bool IsHomeChanged(CreatureContext context)
        {
            Vector3 delta = context.HomePosition - _homeAtSelection;
            delta.y = 0f;
            return delta.sqrMagnitude > HomeChangeEpsilon * HomeChangeEpsilon;
        }

        private float GetSpeed()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.PatrolSpeed) : DefaultSpeed;
        }

        private float GetRadius()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.PatrolRadius) : 10f;
        }

        private float GetWaitChance()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Clamp01(config.PatrolWaitChance) : DefaultWaitChance;
        }

        private float GetWaitDuration()
        {
            CreatureConfig config = Machine.Context.Config;
            float min = config != null ? Mathf.Max(0f, config.PatrolWaitMin) : DefaultWaitMin;
            float max = config != null ? Mathf.Max(0f, config.PatrolWaitMax) : DefaultWaitMax;
            if (max < min)
            {
                (min, max) = (max, min);
            }

            return Random.Range(min, max);
        }
    }
}
