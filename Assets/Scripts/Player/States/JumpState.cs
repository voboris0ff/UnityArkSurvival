namespace ArkSurvival.Player.States
{
    public sealed class JumpState : PlayerState
    {
        public JumpState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
        {
        }

        public override void Enter()
        {
            if (Context.Motor.TryJump())
            {
                Context.ConsumeJumpBuffer();
            }
        }

        public override void Tick(float deltaTime)
        {
            if (Context.Motor.VerticalVelocity <= 0f)
            {
                Machine.ChangeState(Machine.Fall);
                return;
            }

            MoveAir(deltaTime);
        }
    }
}