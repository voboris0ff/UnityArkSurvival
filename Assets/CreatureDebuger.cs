using UnityEngine;
using UnityEngine.InputSystem;

namespace ArkSurvival.AI.Creatures
{
    public sealed class CreatureDebugInput : MonoBehaviour
    {
        [SerializeField] private CreatureController creature;

        private void Update()
        {
            if (Keyboard.current == null ||
                !Keyboard.current.rKey.wasPressedThisFrame)
                return;

            if (creature == null)
            {
                Debug.LogWarning("CreatureController не назначен.");
                return;
            }

            creature.RequestReturn();
        }
    }
}