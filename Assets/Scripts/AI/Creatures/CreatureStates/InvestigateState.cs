using UnityEngine;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Осмотр: существо идёт к последней известной позиции цели, пока цель не видна.
    /// Переходы задаёт машина состояний по восприятию; это состояние только исполняет маршрут.
    /// Первая версия идёт один раз к точке из памяти и не перестраивает путь каждый кадр —
    /// полноценный поиск вокруг точки и завершение по таймеру относятся к DINO-013/DINO-014.
    /// </summary>
    public sealed class InvestigateState : CreatureState
    {
        private const float PointSampleRadius = 1f;

        private bool _destinationAssigned;
        private Transform _trackedTarget;
        private Vector3 _destination;

        public InvestigateState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            _destinationAssigned = false;
            _trackedTarget = null;

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (!navigator.IsReady)
            {
                Debug.LogWarning("InvestigateState: NavMeshAgent не готов — осмотр невозможен.", navigator.Agent);
                return;
            }

            navigator.ResetToSpeed();
            MoveTo(Machine.Context.Memory.LastKnownPosition);
        }

        public override void Exit()
        {
            _destinationAssigned = false;
            _trackedTarget = null;
        }

        public override void Tick(float deltaTime)
        {
            // Percepion уже переключил состояние (увидел цель → Chase, память истекла → Idle);
            // маршрут назначать не нужно, чтобы не перезаписать путь нового состояния.
            if (Machine.Current != this)
            {
                return;
            }

            CreatureContext context = Machine.Context;

            if (!context.Memory.HasTarget)
            {
                Debug.Log("InvestigateState: память о цели истекла — осмотр завершён.", context.Navigator.Agent);
                Machine.ChangeState(Machine.Idle);
                return;
            }

            // Пока цель в поле зрения, точку из памяти не используем: погоню ведёт Chase.
            if (context.Perception.TargetVisible)
            {
                return;
            }

            // Цель отходит во время осмотра: позицию памяти двигаю я, переход в состояние решает машина.
            if (context.Perception.Target != null)
            {
                context.Memory.UpdatePosition(context.Perception.Target, context.Perception.Target.position);
            }

            Vector3 lastKnown = context.Memory.LastKnownPosition;
            if (Vector3.Distance(lastKnown, _destination) > context.Navigator.Agent.stoppingDistance)
            {
                _trackedTarget = context.Memory.Target;
                MoveTo(lastKnown);
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
                Machine.ChangeState(Machine.Idle);
                return;
            }

            if (!context.Navigator.IsPathActive)
            {
                Debug.LogWarning(
                    $"InvestigateState: путь к последней известной позиции {lastKnown} не активен — осмотр завершён.",
                    context.Navigator.Agent);
                Machine.ChangeState(Machine.Idle);
            }
        }

        private void MoveTo(Vector3 position)
        {
            CreatureNavigator navigator = Machine.Context.Navigator;
            _destination = position;
            _destinationAssigned = navigator.TrySetDestination(position, false, PointSampleRadius);

            if (!_destinationAssigned)
            {
                Debug.LogWarning(
                    $"InvestigateState: не удалось назначить путь к последней известной позиции {position}.",
                    navigator.Agent);
            }
        }
    }
}
