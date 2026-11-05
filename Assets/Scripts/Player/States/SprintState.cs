namespace ArkSurvival.Player.States
{
    public sealed class SprintState : PlayerState
    {
        public SprintState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
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

            if (Context.Crouch.IsCrouched || !Context.SprintHeld || !Context.Stamina.CanContinueSprint)
            {
                Machine.ChangeState(Machine.Walk);
                return;
            }

            Context.Stamina.DrainSprint(deltaTime);
            if (!Context.Stamina.CanContinueSprint)
            {
                Machine.ChangeState(Machine.Walk);
                return;
            }

            Move(Context.Config.SprintSpeed, deltaTime);
        }
    }
}