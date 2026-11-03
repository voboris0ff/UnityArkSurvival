using ArkSurvival.AI.Creatures.CreatureStates;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Набор состояний поведения существа. Пока есть Idle и Return;
    /// остальные состояния добавляются отдельными задачами вместе со своими переходами.
    /// </summary>
    public sealed class CreatureStateMachine
    {
        private CreatureState _current;

        public CreatureContext Context { get; }
        public IdleState Idle { get; }
        public ReturnState Return { get; }

        public CreatureState Current => _current;
        public string CurrentName => _current != null ? _current.GetType().Name : string.Empty;

        public CreatureStateMachine(CreatureContext context)
        {
            Context = context;
            Idle = new IdleState(this);
            Return = new ReturnState(this, context.Navigator.Agent, context.HomePosition);
        }

        public void Start()
        {
            ChangeState(Idle);
        }

        public void Tick(float deltaTime)
        {
            _current?.Tick(deltaTime);
        }

        /// <summary>Команда вернуться к исходной точке. Повторный вызов во время возврата ничего не меняет.</summary>
        public void RequestReturn()
        {
            if (_current == Return)
            {
                return;
            }

            ChangeState(Return);
        }

        public void ChangeState(CreatureState next)
        {
            if (next == null || next == _current)
            {
                return;
            }

            _current?.Exit();
            _current = next;
            _current.Enter();
        }
    }
}
