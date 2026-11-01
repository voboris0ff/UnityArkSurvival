namespace ArkSurvival.Player.States
{
    public sealed class FallState : PlayerState
    {
        public FallState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (Context.Input.JumpPressed && Context.Motor.CanJump)
            {
                Machine.ChangeState(Machine.Jump);
                return;
            }

            if (Context.Motor.IsGrounded)
            {
                Machine.ChangeState(ResolveGroundedState());
                return;
            }

            MoveAir(deltaTime);
        }

        private PlayerState ResolveGroundedState()
        {
            if (!Context.HasMove)
            {
                return Machine.Idle;
            }

            return Context.Input.SprintHeld ? Machine.Sprint : Machine.Walk;
        }
    }
}