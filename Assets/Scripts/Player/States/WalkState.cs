namespace ArkSurvival.Player.States
{
    public sealed class WalkState : PlayerState
    {
        public WalkState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
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
                Machine.ChangeState(Machine.Idle);
                return;
            }

            if (Context.Input.SprintHeld)
            {
                Machine.ChangeState(Machine.Sprint);
                return;
            }

            Move(Context.Config.WalkSpeed, deltaTime);
        }
    }
}