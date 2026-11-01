using UnityEngine;

namespace ArkSurvival.Data
{
    [CreateAssetMenu(fileName = "PlayerStaminaConfig", menuName = "ArkSurvival/Player Stamina Config")]
    public sealed class PlayerStaminaConfig : ScriptableObject
    {
        [SerializeField] private float _maxStamina = 100f;
        [SerializeField] private float _sprintDrainPerSecond = 15f;
        [SerializeField] private float _regenPerSecond = 20f;
        [SerializeField] private float _regenDelay = 1f;
        [SerializeField] private float _sprintMinStamina = 10f;
        [SerializeField] private float _rollCost = 20f;
        [SerializeField] private float _exhaustedRecoveryThreshold = 30f;

        public float MaxStamina => _maxStamina;
        public float SprintDrainPerSecond => _sprintDrainPerSecond;
        public float RegenPerSecond => _regenPerSecond;
        public float RegenDelay => _regenDelay;
        public float SprintMinStamina => _sprintMinStamina;
        public float RollCost => _rollCost;
        public float ExhaustedRecoveryThreshold => _exhaustedRecoveryThreshold;
    }
}