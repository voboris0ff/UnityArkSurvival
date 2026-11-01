using UnityEngine;

namespace ArkSurvival.Player.States
{
    public sealed class RollState : PlayerState
    {
        private Vector3 _direction;
        private float _elapsed;

        public RollState(PlayerContext context, PlayerStateMachine machine) : base(context, machine)
        {
        }

        public override void Enter()
        {
            _elapsed = 0f;
            _direction = ResolveDirection();
            Context.Motor.FaceDirection(_direction);
            Context.Stamina.TrySpendRoll();
            Context.StartRollCooldown();
            UpdateInvulnerability();
        }

        public override void Exit()
        {
            Context.IsInvulnerable = false;
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            UpdateInvulnerability();

            if (_elapsed >= Context.Config.RollDuration)
            {
                Machine.ChangeState(ResolveGroundedState());
                return;
            }

            if (!Context.Motor.IsGrounded)
            {
                Machine.ChangeState(Machine.Fall);
                return;
            }

            float progress = Mathf.Clamp01(_elapsed / Context.Config.RollDuration);
            float speed = Context.Config.RollSpeed * Context.Config.RollSpeedCurve.Evaluate(progress);
            Context.Motor.MoveInstant(_direction, speed, deltaTime);
        }

        private void UpdateInvulnerability()
        {
            Context.IsInvulnerable = _elapsed >= Context.Config.IFramesStart
                && _elapsed <= Context.Config.IFramesEnd;
        }

        private Vector3 ResolveDirection()
        {
            Vector3 direction = Context.HasMove ? Context.MoveDirection : Context.CameraForward;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Context.PlayerTransform.forward;
        }
    }
}