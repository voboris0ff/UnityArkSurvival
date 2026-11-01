using UnityEngine;

namespace ArkSurvival.Player.States
{
    public abstract class PlayerState
    {
        protected readonly PlayerContext Context;
        protected readonly PlayerStateMachine Machine;

        protected PlayerState(PlayerContext context, PlayerStateMachine machine)
        {
            Context = context;
            Machine = machine;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public abstract void Tick(float deltaTime);

        protected void Move(float speed, float deltaTime)
        {
            Context.Motor.Move(Context.MoveDirection, speed, deltaTime);
        }

        protected void Stop(float deltaTime)
        {
            Context.Motor.Move(Vector3.zero, 0f, deltaTime);
        }

        protected bool TryLeaveGround()
        {
            if (Context.Input.JumpPressed && Context.Motor.CanJump)
            {
                Machine.ChangeState(Machine.Jump);
                return true;
            }

            if (!Context.Motor.IsGrounded)
            {
                Machine.ChangeState(Machine.Fall);
                return true;
            }

            return false;
        }

        protected void MoveAir(float deltaTime)
        {
            bool hasMove = Context.HasMove;
            Vector3 direction = hasMove ? Context.MoveDirection : Context.PlayerTransform.forward;
            float speed = hasMove
                ? (Context.Input.SprintHeld ? Context.Config.SprintSpeed : Context.Config.WalkSpeed)
                : 0f;
            Context.Motor.Move(direction, speed, deltaTime, Context.Config.AirControl);
        }
    }
}