using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerContext
    {
        public PlayerInputReader Input { get; }
        public CharacterMotor Motor { get; }
        public PlayerMovementConfig Config { get; }
        public PlayerStamina Stamina { get; }
        public PlayerCrouchController Crouch { get; }
        public PlayerCamera Camera { get; }
        public Transform PlayerTransform { get; }

        public PlayerContext(
            PlayerInputReader input,
            CharacterMotor motor,
            PlayerMovementConfig config,
            PlayerStaminaConfig staminaConfig,
            PlayerCrouchConfig crouchConfig,
            PlayerCamera camera,
            Transform playerTransform)
        {
            Input = input;
            Motor = motor;
            Config = config;
            Stamina = new PlayerStamina(staminaConfig);
            Crouch = new PlayerCrouchController(input, crouchConfig, motor.Controller, playerTransform);
            Camera = camera;
            PlayerTransform = playerTransform;
        }

        public bool IsInvulnerable => InvulnerabilityRemaining > 0f;
        public float InvulnerabilityRemaining { get; private set; }
        public float RollCooldownRemaining { get; private set; }
        public float JumpBufferRemaining { get; private set; }
        public bool CanRoll => RollCooldownRemaining <= 0f;
        public bool HasJumpBuffered => JumpBufferRemaining > 0f;

        public bool HasMove => MoveDirection.sqrMagnitude > 0.0001f;

        public Vector3 CameraForward => YawRotation * Vector3.forward;

        public Vector3 MoveDirection
        {
            get
            {
                Vector2 move = Input.Move;
                if (move.sqrMagnitude > 1f)
                {
                    move.Normalize();
                }

                return YawRotation * new Vector3(move.x, 0f, move.y);
            }
        }

        private Quaternion YawRotation
        {
            get
            {
                float yaw = Camera != null ? Camera.Yaw : PlayerTransform.eulerAngles.y;
                return Quaternion.Euler(0f, yaw, 0f);
            }
        }

        public void Tick(float deltaTime)
        {
            if (InvulnerabilityRemaining > 0f)
            {
                InvulnerabilityRemaining = Mathf.Max(0f, InvulnerabilityRemaining - deltaTime);
            }

            if (RollCooldownRemaining > 0f)
            {
                RollCooldownRemaining = Mathf.Max(0f, RollCooldownRemaining - deltaTime);
            }

            if (JumpBufferRemaining > 0f)
            {
                JumpBufferRemaining = Mathf.Max(0f, JumpBufferRemaining - deltaTime);
            }
        }

        public void BufferJump()
        {
            JumpBufferRemaining = Config.JumpBufferTime;
        }

        public void ConsumeJumpBuffer()
        {
            JumpBufferRemaining = 0f;
        }

        public void StartRollCooldown()
        {
            RollCooldownRemaining = Config.RollCooldown;
        }

        public void StartInvulnerability(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            InvulnerabilityRemaining = Mathf.Max(InvulnerabilityRemaining, duration);
        }

        public void ClearInvulnerability()
        {
            InvulnerabilityRemaining = 0f;
        }
    }
}