using UnityEngine;
using UnityEngine.AI;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Динамическая домашняя точка существа. Хранит позицию, привязанную к NavMesh,
    /// и позволяет менять её командой <see cref="SetHome"/>. Не решает, когда менять точку:
    /// это делают состояния (неудачный поиск — <see cref="CreatureStates.SearchState"/>), а патруль
    /// вокруг точки ведёт <see cref="CreatureStates.PatrolState"/> (DINO-018).
    /// </summary>
    public sealed class CreatureHome
    {
        private const float MinSampleRadius = 0.1f;
        private const float DefaultSampleRadius = 2f;
        private const float ChangeEpsilon = 0.0001f;

        private readonly NavMeshAgent _agent;
        private Vector3 _position;
        private bool _isInitialized;

        public CreatureHome(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public Vector3 Position => _position;
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Начальная домашняя точка. Вызывается один раз при старте; позиция фиксируется
        /// и больше не следует за существом. Если NavMesh рядом нет, точка всё равно
        /// задаётся «как есть»: <see cref="CreatureStates.ReturnState"/> затем проверит достижимость.
        /// </summary>
        public void Initialize(Vector3 position)
        {
            if (TrySnapToNavMesh(position, DefaultSampleRadius, out Vector3 snapped))
            {
                _position = snapped;
            }
            else
            {
                _position = position;
                Debug.LogWarning(
                    $"CreatureHome: точка {position} не привязана к NavMesh — домашняя точка задана без привязки.",
                    _agent);
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Меняет домашнюю точку, привязывая её к NavMesh. Если рядом с позицией нет NavMesh,
        /// точка не меняется и возвращается false.
        /// </summary>
        public bool SetHome(Vector3 position, float sampleRadius = DefaultSampleRadius)
        {
            if (!TrySnapToNavMesh(position, sampleRadius, out Vector3 snapped))
            {
                Debug.LogWarning(
                    $"CreatureHome: поблизости от {position} нет NavMesh — домашняя точка не изменена ({_position}).",
                    _agent);
                return false;
            }

            Vector3 previous = _position;
            _position = snapped;
            _isInitialized = true;

            if ((previous - snapped).sqrMagnitude > ChangeEpsilon)
            {
                Debug.Log($"CreatureHome: домашняя точка {previous} → {snapped}.", _agent);
            }

            return true;
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

            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, Mathf.Max(sampleRadius, MinSampleRadius), filter))
            {
                return false;
            }

            result = hit.position;
            return true;
        }
    }
}
