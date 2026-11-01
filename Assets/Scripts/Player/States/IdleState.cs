namespace ArkSurvival.Player.States
{
    public sealed class IdleState : PlayerState
    {
        public IdleState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (TryLeaveGround())
            {
                return;
            }

            if (!Context.HasMove)
            {
                Stop(deltaTime);
                return;
            }

            Machine.ChangeState(Context.Input.SprintHeld ? Machine.Sprint : Machine.Walk);
        }
    }
}