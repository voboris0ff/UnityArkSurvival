using UnityEngine;
using UnityEngine.InputSystem;

namespace ArkSurvival.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionReference _moveAction;
        [SerializeField] private InputActionReference _lookAction;
        [SerializeField] private InputActionReference _sprintAction;
        [SerializeField] private InputActionReference _jumpAction;
        [SerializeField] private InputActionReference _rollAction;

        public Vector2 Move => ReadVector2(_moveAction);
        public Vector2 Look => ReadVector2(_lookAction);
        public bool SprintHeld => IsPressed(_sprintAction);
        public bool JumpPressed => WasPressedThisFrame(_jumpAction);
        public bool JumpHeld => IsPressed(_jumpAction);
        public bool RollPressed => WasPressedThisFrame(_rollAction);

        private void Awake()
        {
            Enable(_moveAction);
            Enable(_lookAction);
            Enable(_sprintAction);
            Enable(_jumpAction);
            Enable(_rollAction);
        }

        private void OnDestroy()
        {
            Disable(_moveAction);
            Disable(_lookAction);
            Disable(_sprintAction);
            Disable(_jumpAction);
            Disable(_rollAction);
        }

        private static Vector2 ReadVector2(InputActionReference reference)
        {
            return reference != null && reference.action != null
                ? reference.action.ReadValue<Vector2>()
                : Vector2.zero;
        }

        private static bool IsPressed(InputActionReference reference)
        {
            return reference != null && reference.action != null && reference.action.IsPressed();
        }

        private static bool WasPressedThisFrame(InputActionReference reference)
        {
            return reference != null && reference.action != null && reference.action.WasPressedThisFrame();
        }

        private static void Enable(InputActionReference reference)
        {
            if (reference != null && reference.action != null)
            {
                reference.action.Enable();
            }
        }

        private static void Disable(InputActionReference reference)
        {
            if (reference != null && reference.action != null)
            {
                reference.action.Disable();
            }
        }
    }
}