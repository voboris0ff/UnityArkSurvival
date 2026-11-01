namespace ArkSurvival.Player.States
{
    public sealed class SprintState : PlayerState
    {
        public SprintState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
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

            if (!Context.Input.SprintHeld)
            {
                Machine.ChangeState(Machine.Walk);
                return;
            }

            Move(Context.Config.SprintSpeed, deltaTime);
        }
    }
}