using UnityEngine;
using UnityEngine.AI;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Возврат к исходной точке существа. Сначала проверяет достижимость точки, затем один раз
    /// назначает destination; при прибытии возвращается в Idle.
    /// </summary>
    public sealed class ReturnState : CreatureState
    {
        private const float SampleRadius = 2f;

        private readonly NavMeshAgent _agent;
        private readonly Vector3 _homePosition;
        private bool _destinationAssigned;
        private bool _failed;

        public ReturnState(CreatureStateMachine machine, NavMeshAgent agent, Vector3 homePosition) : base(machine)
        {
            _agent = agent;
            _homePosition = homePosition;
        }

        public Vector3 HomePosition => _homePosition;

        public override void Enter()
        {
            _destinationAssigned = false;
            _failed = false;

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (!navigator.IsValid || !navigator.CanReach(_homePosition, SampleRadius))
            {
                Debug.LogWarning($"ReturnState: исходная точка {_homePosition} недостижима.", _agent);
                _failed = true;
                return;
            }

            navigator.ResetToSpeed();
            _destinationAssigned = navigator.TrySetDestination(_homePosition, false, SampleRadius);

            if (!_destinationAssigned)
            {
                Debug.LogWarning($"ReturnState: не удалось назначить путь к исходной точке {_homePosition}.", _agent);
                _failed = true;
                return;
            }

            Debug.Log($"ReturnState: путь к исходной точке {_homePosition} назначен.", _agent);
        }

        public override void Exit()
        {
            _destinationAssigned = false;
            _failed = false;
        }

        public override void Tick(float deltaTime)
        {
            if (_failed)
            {
                Debug.LogWarning("ReturnState: возврат отменён, переход в Idle.", _agent);
                Machine.ChangeState(Machine.Idle);
                return;
            }

            CreatureNavigator navigator = Machine.Context.Navigator;
            if (navigator.HasArrived)
            {
                Debug.Log("ReturnState: прибыл к исходной точке.", _agent);
                Machine.ChangeState(Machine.Idle);
                return;
            }

            if (!navigator.IsPathActive)
            {
                NavMeshAgent agent = navigator.Agent;
                Debug.LogWarning(
                    "ReturnState: путь пропал, переход в Idle. " +
                    $"status={agent.pathStatus}, pathPending={agent.pathPending}, hasPath={agent.hasPath}, " +
                    $"isStopped={agent.isStopped}, isOnNavMesh={agent.isOnNavMesh}, " +
                    $"remaining={agent.remainingDistance:F2}, destination={agent.destination}, " +
                    $"position={agent.transform.position}, corners={agent.path.corners.Length}",
                    _agent);
                Machine.ChangeState(Machine.Idle);
            }
        }
    }
}
