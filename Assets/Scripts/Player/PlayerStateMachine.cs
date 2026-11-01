using ArkSurvival.Player.States;

namespace ArkSurvival.Player
{
    public sealed class PlayerStateMachine
    {
        private PlayerState _current;

        public IdleState Idle { get; }
        public WalkState Walk { get; }
        public SprintState Sprint { get; }
        public JumpState Jump { get; }
        public FallState Fall { get; }

        public PlayerState Current => _current;
        public string CurrentName => _current != null ? _current.GetType().Name : string.Empty;

        public PlayerStateMachine(PlayerContext context)
        {
            Idle = new IdleState(context, this);
            Walk = new WalkState(context, this);
            Sprint = new SprintState(context, this);
            Jump = new JumpState(context, this);
            Fall = new FallState(context, this);
        }

        public void Start()
        {
            ChangeState(Idle);
        }

        public void Tick(float deltaTime)
        {
            _current?.Tick(deltaTime);
        }

        public void ChangeState(PlayerState next)
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