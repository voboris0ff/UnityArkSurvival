using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Осмотр: существо идёт к позиции, где цель была замечена (позже — услышана).
    /// Точка берётся из памяти и не «подглядывает» за невидимой целью: память обновляет только
    /// реально видимая цель (это делает <see cref="ChaseState"/>).
    /// Осмотр завершается по прибытии к точке или по истечении <c>CreatureConfig.InvestigateDuration</c>,
    /// после чего состояние сообщает машине, что память отработана, и уходит в <see cref="SearchState"/>.
    /// Возврат домой после поиска — DINO-016.
    /// </summary>
    public sealed class InvestigateState : CreatureState
    {
        private const float PointSampleRadius = 1f;

        [Tooltip("Насколько должна сдвинуться последняя известная позиция, чтобы путь перестроился, м (временное).")]
        private const float MinRepathDistance = 0.5f;

        private const float DefaultDuration = 8f;

        private bool _destinationAssigned;
        private bool _failed;
        private float _elapsedTime;
        private Vector3 _destination;

        public InvestigateState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            _destinationAssigned = false;
            _failed = false;
            _elapsedTime = 0f;

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (!navigator.IsReady)
            {
                Debug.LogWarning("InvestigateState: NavMeshAgent не готов — осмотр невозможен.", navigator.Agent);
                _failed = true;
                return;
            }

            navigator.ResetToSpeed();
            Vector3 lastKnown = Machine.Context.Memory.LastKnownPosition;
            Debug.Log(
                $"InvestigateState: иду к последней известной позиции {lastKnown}, " +
                $"лимит осмотра {GetDuration():F1} с.",
                navigator.Agent);

            UpdateDestination();
        }

        public override void Exit()
        {
            _destinationAssigned = false;
            _failed = false;
            _elapsedTime = 0f;
        }

        public override void Tick(float deltaTime)
        {
            // Машина могла переключить состояние в тике восприятия — маршрут нового состояния не трогаем.
            if (Machine.Current != this)
            {
                return;
            }

            CreatureContext context = Machine.Context;

            if (!context.Memory.HasTarget)
            {
                Debug.Log("InvestigateState: память о цели истекла — осмотр завершён.", context.Navigator.Agent);
                Finish();
                return;
            }

            if (_failed)
            {
                Debug.LogWarning(
                    "InvestigateState: маршрут не назначен — осмотр завершён.",
                    context.Navigator.Agent);
                Finish();
                return;
            }

            _elapsedTime += Mathf.Max(0f, deltaTime);
            if (_elapsedTime >= GetDuration())
            {
                Debug.Log(
                    $"InvestigateState: лимит осмотра {GetDuration():F1} с истёк — осмотр завершён.",
                    context.Navigator.Agent);
                Finish();
                return;
            }

            Vector3 lastKnown = context.Memory.LastKnownPosition;
            if (ShouldRepath(lastKnown))
            {
                UpdateDestination();
                return;
            }

            if (!_destinationAssigned)
            {
                return;
            }

            if (context.Navigator.HasArrived)
            {
                Debug.Log(
                    $"InvestigateState: прибыл к последней известной позиции {lastKnown} — осмотр завершён.",
                    context.Navigator.Agent);
                Finish();
                return;
            }

            if (!context.Navigator.IsPathActive)
            {
                Debug.LogWarning(
                    $"InvestigateState: путь к последней известной позиции {lastKnown} не активен — осмотр завершён.",
                    context.Navigator.Agent);
                Finish();
            }
        }

        /// <summary>
        /// Осмотр закончен: помечаем память отработанной, чтобы <see cref="CreatureStateMachine.ApplyPerception"/>
        /// не запускал осмотр заново на той же живой памяти (иначе прибытие каждый кадр давало бы новый Enter),
        /// и переходим к поиску вокруг последней известной позиции.
        /// </summary>
        private void Finish()
        {
            Machine.NotifyInvestigationFinished();
            Machine.ChangeState(Machine.Search);
        }

        /// <summary>Последняя известная позиция ушла от текущей точки назначения — путь надо перестроить.</summary>
        private bool ShouldRepath(Vector3 lastKnown)
        {
            float threshold = Mathf.Max(MinRepathDistance, Machine.Context.Navigator.Agent.stoppingDistance);
            Vector3 offset = lastKnown - _destination;
            offset.y = 0f;
            return offset.sqrMagnitude > threshold * threshold;
        }

        private void UpdateDestination()
        {
            CreatureNavigator navigator = Machine.Context.Navigator;
            Vector3 position = Machine.Context.Memory.LastKnownPosition;
            _destination = position;
            _destinationAssigned = navigator.TrySetDestination(position, false, PointSampleRadius);
            _failed = !_destinationAssigned;

            if (!_destinationAssigned)
            {
                Debug.LogWarning(
                    $"InvestigateState: не удалось назначить путь к последней известной позиции {position}.",
                    navigator.Agent);
            }
        }

        private float GetDuration()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.InvestigateDuration) : DefaultDuration;
        }
    }
}
