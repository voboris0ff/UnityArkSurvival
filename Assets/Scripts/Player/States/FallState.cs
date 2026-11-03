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
                if (Context.Crouch.TryStand())
                {
                    Machine.ChangeState(Machine.Jump);
                    return;
                }

                Context.ConsumeJumpBuffer();
            }

            if (Context.Motor.IsGrounded)
            {
                Machine.ChangeState(ResolveGroundedState());
                return;
            }

            MoveAir(deltaTime);

            if (Context.Motor.IsGrounded)
            {
                Machine.ChangeState(ResolveGroundedState());
            }
        }
    }
}