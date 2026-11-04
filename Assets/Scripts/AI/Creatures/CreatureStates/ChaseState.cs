using ArkSurvival.Data;
using UnityEngine;
using UnityEngine.AI;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Погоня: существо идёт за целью на повышенной скорости. Точка назначения обновляется
    /// не каждый кадр и не по таймеру, а только когда цель видна и заметно сместилась от прошлой
    /// точки назначения, но не чаще <c>CreatureConfig.DestinationUpdateInterval</c>.
    /// Когда цель пропадает из виду и память истекает — переход в Investigate/Idle делает машина,
    /// здесь же на всякий случай снимается устаревший маршрут.
    /// Атака и тактика обхода — следующие задачи (DINO-023/DINO-014).
    /// </summary>
    public sealed class ChaseState : CreatureState
    {
        private const float TargetSampleRadius = 1f;

        [Tooltip("Насколько цель должна сдвинуться, чтобы путь перестроился, м (временное).")]
        private const float MinRepathDistance = 0.5f;

        [Tooltip("Дальность прямой видимости цели: вблизи точка назначения — на стоящем агенте, м (временное).")]
        private const float CloseRangeDistance = 3f;

        private bool _destinationAssigned;
        private bool _failed;
        private float _elapsedSinceDestination;
        private Vector3 _destination;

        public ChaseState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            _destinationAssigned = false;
            _failed = false;
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
            CreatureNavigator navigator = context.Navigator;

            if (_failed)
            {
                Debug.Log("ChaseState: маршрут не назначен — погоня завершена, бросаем команду осмотра.", navigator.Agent);
                Machine.RequestInvestigate();
                return;
            }

            bool targetVisible = context.Perception.TargetVisible && context.Perception.Target != null;

            if (!targetVisible || !context.Memory.HasTarget)
            {
                // Переход в Investigate/Idle выполнил ApplyPerception до этого тика — если текущим
                // состоянием всё ещё остались мы, значит маршрут ведёт в неактуальную точку: снимаем его.
                if (navigator.IsPathActive)
                {
                    Debug.Log(
                        "ChaseState: цель потеряна, память не подтверждает погоню — маршрут снят.",
                        navigator.Agent);
                }

                navigator.Stop();
                _destinationAssigned = false;
                return;
            }

            _elapsedSinceDestination += Mathf.Max(0f, deltaTime);

            if (!_destinationAssigned)
            {
                UpdateDestination();
                return;
            }

            // Частичный или невалидный путь отклонил navigator — пробуем ещё раз по интервалу.
            if (!navigator.IsPathActive)
            {
                if (_elapsedSinceDestination < GetUpdateInterval())
                {
                    return;
                }

                UpdateDestination();
                return;
            }

            if (!ShouldRepath(context))
            {
                return;
            }

            if (_elapsedSinceDestination < GetUpdateInterval())
            {
                return;
            }

            UpdateDestination();
        }

        /// <summary>
        /// Перестраивать путь нужно, только если цель ушла от прошлой точки назначения:
        /// агент ходит по NavMesh сам, и не «дёргается» от мелких смещений или от micro-подрагивания.
        /// </summary>
        private bool ShouldRepath(CreatureContext context)
        {
            CreatureNavigator navigator = context.Navigator;
            // Агент мог быть уничтожен или выключен между тиками: не трогаем его поля.
            float stoppingDistance = navigator != null && navigator.IsValid && navigator.IsReady
                ? navigator.Agent.stoppingDistance
                : 0f;
            Vector3 goal = GetDestinationPoint(context.Perception.Target, navigator != null ? navigator.Agent : null);
            float threshold = Mathf.Max(MinRepathDistance, stoppingDistance);

            Vector3 offset = goal - _destination;
            offset.y = 0f;
            return offset.sqrMagnitude > threshold * threshold;
        }

        /// <summary>
        /// Вблизи цель движется скачками (анимация, CharacterController), поэтому в упор точка
        /// назначения ставится на самого агента: он не «пинает» путь при каждом шаге цели.
        /// </summary>
        private Vector3 GetDestinationPoint(Transform target, NavMeshAgent agent)
        {
            Vector3 goal = target.position;
            if (agent == null)
            {
                return goal;
            }

            Vector3 toTarget = goal - agent.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= CloseRangeDistance * CloseRangeDistance)
            {
                return agent.transform.position;
            }

            return goal;
        }

        private float GetUpdateInterval()
        {
            CreatureConfig config = Machine.Context.Config;
            return config != null ? Mathf.Max(0f, config.DestinationUpdateInterval) : 0f;
        }

        private float GetChaseSpeed()
        {
            CreatureConfig config = Machine.Context.Config;
            if (config != null)
            {
                return config.ChaseSpeed;
            }

            CreatureNavigator navigator = Machine.Context.Navigator;
            return navigator != null && navigator.IsReady ? navigator.Agent.speed : 0f;
        }

        private void UpdateDestination()
        {
            CreatureContext context = Machine.Context;
            CreatureNavigator navigator = context.Navigator;
            Transform target = context.Perception.Target;
            if (target == null)
            {
                _failed = true;
                return;
            }

            Vector3 goal = GetDestinationPoint(target, navigator.Agent);
            _elapsedSinceDestination = 0f;
            _destination = goal;
            _destinationAssigned = navigator.TrySetDestination(goal, false, TargetSampleRadius);
            _failed = !_destinationAssigned;

            if (!_destinationAssigned)
            {
                Debug.LogWarning(
                    $"ChaseState: не удалось назначить путь к точке {goal}.",
                    navigator.Agent);
                return;
            }

            // Наблюдение фиксирует только реально видимая цель: точка за стеной память не обновляет.
            if (context.Perception.TargetVisible)
            {
                context.Memory.Observe(target, target.position);
            }
            else
            {
                context.Memory.UpdatePosition(target, target.position);
            }
        }
    }
}
