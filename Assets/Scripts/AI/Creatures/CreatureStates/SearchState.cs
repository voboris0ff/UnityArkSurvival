using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Поиск: после осмотра существо ограниченное время обходит случайные достижимые точки
    /// вокруг последней известной позиции цели, останавливаясь на каждой точке на короткое время.
    /// Переход в Chase при повторном обнаружении игрока выполняет только
    /// <see cref="CreatureStateMachine.ApplyPerception"/>; само состояние восприятие не читает.
    /// Сложная тактика обхода укрытий не реализуется. Неудачное завершение поиска переносит
    /// домашнюю точку на место окончания погони, забывает цель и ведёт в Idle.
    /// </summary>
    public sealed class SearchState : CreatureState
    {
        private const float PointSampleRadius = 2f;

        [Tooltip("Минимальное расстояние от текущей позиции до новой точки поиска, м (временное).")]
        private const float MinPointDistance = 1.5f;

        [Tooltip("Сколько попыток подобрать достижимую точку за один раз (временное).")]
        private const int MaxPointAttempts = 8;

        [Tooltip("Пауза между неудачными попытками выбрать точку, с (временное).")]
        private const float RetryDelay = 0.5f;

        private const float DefaultDuration = 6f;
        private const float DefaultRadius = 8f;
        private const float DefaultStopDuration = 1f;

        private Vector3 _anchor;
        private float _elapsedTime;
        private float _waitRemaining;
        private float _retryRemaining;
        private bool _waiting;
        private bool _destinationAssigned;

        public SearchState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            _elapsedTime = 0f;
            _waitRemaining = 0f;
            _retryRemaining = 0f;
            _waiting = false;
            _destinationAssigned = false;
            // Опора поиска фиксируется на входе: позиция из памяти не должна «уезжать»,
            // пока цель не видна, иначе область поиска смещалась бы вслед за существом.
            _anchor = Machine.Context.Memory.LastKnownPosition;

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (!navigator.IsReady)
            {
                Debug.LogWarning("SearchState: NavMeshAgent не готов — поиск невозможен.", navigator.Agent);
                return;
            }

            navigator.ResetToSpeed();
            Debug.Log(
                $"SearchState: поиск вокруг {_anchor} радиусом {GetRadius():F1} м, " +
                $"лимит {GetDuration():F1} с.",
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
            // Машина могла переключить состояние в тике восприятия (вижу цель → Chase) — маршрут не трогаем.
            if (Machine.Current != this)
            {
                return;
            }

            float delta = Mathf.Max(0f, deltaTime);
            _elapsedTime += delta;

            if (_elapsedTime >= GetDuration())
            {
                Debug.Log($"SearchState: лимит поиска {GetDuration():F1} с истёк — поиск завершён.", Machine.Context.Navigator.Agent);
                Finish();
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

            CreatureNavigator navigator = Machine.Context.Navigator;

            if (!_destinationAssigned)
            {
                _retryRemaining -= delta;
                if (_retryRemaining <= 0f)
                {
                    PickNextPoint();
                }

                return;
            }

            if (navigator.HasArrived)
            {
                // Короткая остановка на точке: осмотреться, затем идти к следующей.
                navigator.Stop();
                _destinationAssigned = false;
                _waiting = true;
                _waitRemaining = GetStopDuration();
                return;
            }

            // Путь до случайной точки мог не построиться или пропасть — выбираем другую.
            if (!navigator.IsPathActive)
            {
                _destinationAssigned = false;
                _retryRemaining = RetryDelay;
            }
        }

        /// <summary>
        /// Погоня неудачна: цель не обнаружена за время поиска. Место, где существо прекратило
        /// погоню, становится новой домашней точкой, память о цели сбрасывается, затем — Patrol
        /// (DINO-016/DINO-018): новые точки патруля выбираются вокруг новой домашней точки.
        /// Отдельного состояния возврата к прежней точке не создаётся.
        /// </summary>
        private void Finish()
        {
            CreatureContext context = Machine.Context;
            Vector3 stopPosition = context.Navigator.Position;

            if (context.Home != null && context.Home.SetHome(stopPosition))
            {
                Debug.Log(
                    $"SearchState: цель не найдена — домашняя точка перенесена на {context.Home.Position}.",
                    context.Navigator.Agent);
            }
            else
            {
                Debug.LogWarning(
                    $"SearchState: цель не найдена, но домашнюю точку на {stopPosition} перенести не удалось.",
                    context.Navigator.Agent);
            }

            context.Memory.Clear();
            Machine.NotifySearchFinished();
            Machine.ChangeState(Machine.Patrol);
        }

        private void PickNextPoint()
        {
            CreatureContext context = Machine.Context;
            CreatureNavigator navigator = context.Navigator;

            for (int attempt = 0; attempt < MaxPointAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * GetRadius();
                Vector3 candidate = _anchor + new Vector3(offset.x, 0f, offset.y);

                Vector3 fromCurrent = candidate - navigator.Position;
                fromCurrent.y = 0f;
                if (fromCurrent.sqrMagnitude < MinPointDistance * MinPointDistance)
                {
                    continue;
                }

                if (!navigator.CanReach(candidate, PointSampleRadius))
                {
                    continue;
                }

                if (navigator.TrySetDestination(candidate, false, PointSampleRadius))
                {
                    _destinationAssigned = true;
                    _retryRemaining = 0f;
                    return;
                }
            }

            _destinationAssigned = false;
            _retryRemaining = RetryDelay;

            if (navigator.IsReady)
            {
                navigator.Stop();
            }
        }

        private float GetDuration()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.SearchDuration) : DefaultDuration;
        }

        private float GetRadius()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.SearchRadius) : DefaultRadius;
        }

        private float GetStopDuration()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.SearchStopDuration) : DefaultStopDuration;
        }
    }
}
