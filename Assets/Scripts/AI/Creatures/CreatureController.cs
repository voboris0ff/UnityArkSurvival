using ArkSurvival.Data;
using UnityEngine;
using UnityEngine.AI;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Точка композиции существа: связывает компоненты, создаёт навигационный адаптер,
    /// валидирует ссылки и тикает машину состояний. Поведением не управляет — решения в состояниях.
    /// </summary>
    public sealed class CreatureController : MonoBehaviour
    {
        [SerializeField] private CreatureConfig _config;
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private Transform _homePoint;

        private CreatureNavigator _navigator;
        private CreatureStateMachine _stateMachine;
        private Vector3 _homePosition;

        public CreatureConfig Config => _config;
        public CreatureNavigator Navigator => _navigator;
        public CreatureStateMachine StateMachine => _stateMachine;
        public Transform HomePoint => _homePoint;
        public Vector3 HomePosition => _homePosition;
        public bool IsInitialized => _navigator != null && _navigator.IsValid;

        private void Awake()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (_homePoint == null)
            {
                _homePoint = transform;
            }
        }

        private void Start()
        {
            // Позиция дома фиксируется при старте: иначе без _homePoint она едет вместе с существом.
            _homePosition = _homePoint.position;

            if (!ValidateReferences())
            {
                return;
            }

            _navigator = new CreatureNavigator(_agent);
            _navigator.Initialize(_config.ArrivalDistance, _config.MoveSpeed);

            if (!_navigator.IsReady)
            {
                Debug.LogError(
                    "CreatureController: NavMeshAgent не активен или существо не стоит на NavMesh. " +
                    "Проверьте, что NavMesh запечён и объект находится на поверхности.",
                    this);
            }

            _stateMachine = new CreatureStateMachine(new CreatureContext(_navigator, HomePosition));
            _stateMachine.Start();
        }

        /// <summary>Команда вернуться к исходной точке. Реакция на события (страх, хп и т.п.) — отдельная задача.</summary>
        public void RequestReturn()
        {
            if (_stateMachine == null)
            {
                Debug.LogWarning(
                    "CreatureController: RequestReturn вызван до инициализации (Start не завершён) — команда проигнорирована.",
                    this);
                return;
            }

            if (!_navigator.IsReady)
            {
                Debug.LogWarning(
                    "CreatureController: RequestReturn — NavMeshAgent не готов (нет NavMesh или агент выключен).",
                    this);
                return;
            }

            Debug.Log($"CreatureController: запрос возврата, текущее состояние {_stateMachine.CurrentName}.", this);
            _stateMachine.RequestReturn();
            Debug.Log($"CreatureController: новое состояние {_stateMachine.CurrentName}.", this);
        }

        private void Update()
        {
            if (_stateMachine == null)
            {
                return;
            }

            _stateMachine.Tick(Time.deltaTime);
        }

        private bool ValidateReferences()
        {
            if (_config == null)
            {
                Debug.LogError("CreatureController: CreatureConfig не назначен — существо не запущено.", this);
                return false;
            }

            if (_agent == null)
            {
                Debug.LogError("CreatureController: NavMeshAgent не найден — существо не запущено.", this);
                return false;
            }

            return true;
        }
    }
}
