using ArkSurvival.Data;
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
            MoveInternal(Context.MoveDirection, speed, deltaTime);
        }

        protected void Stop(float deltaTime)
        {
            MoveInternal(Vector3.zero, 0f, deltaTime);
        }

        protected bool TryStartRoll()
        {
            if (!Context.Input.RollPressed
                || !Context.CanRoll
                || !Context.Motor.IsGrounded
                || !Context.Stamina.CanSpendRoll)
            {
                return false;
            }

            if (!Context.Crouch.TryStand())
            {
                return false;
            }

            Machine.ChangeState(Machine.Roll);
            return true;
        }

        protected PlayerState ResolveGroundedState()
        {
            if (!Context.HasMove)
            {
                return Machine.Idle;
            }

            if (Context.Crouch.IsCrouched)
            {
                return Machine.Walk;
            }

            return Context.Input.SprintHeld && Context.Stamina.CanStartSprint
                ? Machine.Sprint
                : Machine.Walk;
        }

        protected bool TryLeaveGround()
        {
            if ((Context.Input.JumpPressed || Context.HasJumpBuffered) && Context.Motor.CanJump)
            {
                if (Context.Crouch.TryStand())
                {
                    Machine.ChangeState(Machine.Jump);
                    return true;
                }

                Context.ConsumeJumpBuffer();
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
            float speed = 0f;
            if (hasMove)
            {
                PlayerCrouchConfig crouchConfig = Context.Crouch.Config;
                if (Context.Crouch.IsCrouched && crouchConfig != null)
                {
                    speed = crouchConfig.CrouchSpeed;
                }
                else
                {
                    speed = Context.Input.SprintHeld ? Context.Config.SprintSpeed : Context.Config.WalkSpeed;
                }
            }

            Context.Motor.Move(direction, speed, deltaTime, Context.Config.AirControl);
        }

        private void MoveInternal(Vector3 direction, float speed, float deltaTime)
        {
            PlayerCrouchConfig crouchConfig = Context.Crouch.Config;
            if (Context.Crouch.IsCrouched && crouchConfig != null)
            {
                float targetSpeed = speed > 0.0001f ? crouchConfig.CrouchSpeed : 0f;
                Context.Motor.Move(direction, targetSpeed, deltaTime, 1f,
                    crouchConfig.CrouchAcceleration, crouchConfig.CrouchDeceleration);
                return;
            }

            Context.Motor.Move(direction, speed, deltaTime);
        }
    }
}