using UnityEngine;

namespace ArkSurvival.Data
{
    public enum CrouchInputMode
    {
        Toggle,
        Hold
    }

    [CreateAssetMenu(fileName = "PlayerCrouchConfig", menuName = "ArkSurvival/Player Crouch Config")]
    public sealed class PlayerCrouchConfig : ScriptableObject
    {
        [SerializeField] private CrouchInputMode _crouchInputMode = CrouchInputMode.Toggle;
        [SerializeField] private float _crouchHeight = 1f;
        [SerializeField] private float _crouchCenterY = 0.5f;
        [SerializeField] private float _heightTransitionSpeed = 10f;
        [SerializeField] private float _ceilingSkin = 0.02f;
        [SerializeField] private LayerMask _ceilingLayerMask = ~0;
        [SerializeField] private float _crouchSpeed = 2.5f;
        [SerializeField] private float _crouchAcceleration = 20f;
        [SerializeField] private float _crouchDeceleration = 25f;
        [SerializeField] private float _crouchStepOffset = 0.25f;

        public CrouchInputMode InputMode => _crouchInputMode;
        public float CrouchHeight => _crouchHeight;
        public float CrouchCenterY => _crouchCenterY;
        public float HeightTransitionSpeed => _heightTransitionSpeed;
        public float CeilingSkin => _ceilingSkin;
        public LayerMask CeilingLayerMask => _ceilingLayerMask;
        public float CrouchSpeed => _crouchSpeed;
        public float CrouchAcceleration => _crouchAcceleration;
        public float CrouchDeceleration => _crouchDeceleration;
        public float CrouchStepOffset => _crouchStepOffset;
    }
}