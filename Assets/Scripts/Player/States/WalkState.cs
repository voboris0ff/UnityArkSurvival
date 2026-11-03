namespace ArkSurvival.Player.States
{
    public sealed class WalkState : PlayerState
    {
        public WalkState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (TryStartRoll())
            {
                return;
            }

            if (TryLeaveGround())
            {
                return;
            }

            if (!Context.HasMove)
            {
                Machine.ChangeState(Machine.Idle);
                return;
            }

            if (!Context.Crouch.IsCrouched && Context.Input.SprintHeld && Context.Stamina.CanStartSprint)
            {
                Machine.ChangeState(Machine.Sprint);
                return;
            }

            Move(Context.Config.WalkSpeed, deltaTime);
        }
    }
}