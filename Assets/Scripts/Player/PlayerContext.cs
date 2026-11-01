using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerContext
    {
        public PlayerInputReader Input { get; }
        public CharacterMotor Motor { get; }
        public PlayerMovementConfig Config { get; }
        public PlayerCamera Camera { get; }
        public Transform PlayerTransform { get; }

        public PlayerContext(
            PlayerInputReader input,
            CharacterMotor motor,
            PlayerMovementConfig config,
            PlayerCamera camera,
            Transform playerTransform)
        {
            Input = input;
            Motor = motor;
            Config = config;
            Camera = camera;
            PlayerTransform = playerTransform;
        }

        public bool HasMove => MoveDirection.sqrMagnitude > 0.0001f;

        public Vector3 MoveDirection
        {
            get
            {
                Vector2 move = Input.Move;
                if (move.sqrMagnitude > 1f)
                {
                    move.Normalize();
                }

                float yaw = Camera != null ? Camera.Yaw : PlayerTransform.eulerAngles.y;
                Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
                return yawRotation * new Vector3(move.x, 0f, move.y);
            }
        }
    }
}