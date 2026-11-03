using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Погоня: существо идёт за целью на повышенной скорости. Точка назначения обновляется
    /// не каждый кадр, а раз в <c>CreatureConfig.DestinationUpdateInterval</c>.
    /// Атаки и тактика обхода — следующие задачи (DINO-012+).
    /// </summary>
    public sealed class ChaseState : CreatureState
    {
        private const float TargetSampleRadius = 1f;

        private bool _destinationAssigned;
        private float _elapsedSinceDestination;

        public ChaseState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            _destinationAssigned = false;
            _elapsedSinceDestination = 0f;

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (!navigator.IsReady)
            {
                Debug.LogWarning("ChaseState: NavMeshAgent не готов — погоня невозможна.", navigator.Agent);
                return;
            }

            navigator.SetSpeed(GetChaseSpeed());
            UpdateDestination();
        }

        public override void Exit()
        {
            _destinationAssigned = false;

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (navigator.IsReady)
            {
                navigator.ResetToSpeed();
            }
        }

        public override void Tick(float deltaTime)
        {
            // Машина могла переключить состояние прямо в тике восприятия — маршрут тогда не трогаем.
            if (Machine.Current != this)
            {
                return;
            }

            CreatureContext context = Machine.Context;
            Transform target = context.Perception.Target;

            if (!context.Perception.TargetVisible || target == null)
            {
                // Переход в Investigate делает машина по восприятию; путь оставляем как есть.
                return;
            }

            _elapsedSinceDestination += Mathf.Max(0f, deltaTime);
            if (_destinationAssigned && _elapsedSinceDestination < GetUpdateInterval())
            {
                return;
            }

            UpdateDestination();
        }

        private float GetUpdateInterval()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.DestinationUpdateInterval) : 0f;
        }

        private float GetChaseSpeed()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? config.ChaseSpeed : Machine.Context.Navigator.Agent.speed;
        }

        private void UpdateDestination()
        {
            CreatureContext context = Machine.Context;
            Transform target = context.Perception.Target;
            if (target == null)
            {
                return;
            }

            _elapsedSinceDestination = 0f;
            _destinationAssigned = context.Navigator.TrySetDestination(target.position, false, TargetSampleRadius);

            if (_destinationAssigned)
            {
                context.Memory.Observe(target, target.position);
            }
        }
    }
}
