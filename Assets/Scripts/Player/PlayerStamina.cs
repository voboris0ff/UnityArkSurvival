using System;
using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerStamina
    {
        private readonly PlayerStaminaConfig _config;
        private float _current;
        private float _regenDelayRemaining;
        private bool _exhausted;

        public event Action<float, float> Changed;

        public PlayerStamina(PlayerStaminaConfig config)
        {
            _config = config;
            _current = Max;
        }

        public float Current => _current;
        public float Max => _config != null ? _config.MaxStamina : 0f;
        public float Normalized => Max > 0f ? _current / Max : 0f;
        public bool IsExhausted => _exhausted;

        public bool CanStartSprint => _config != null && !_exhausted && _current >= _config.SprintMinStamina;
        public bool CanContinueSprint => _config != null && !_exhausted && _current > 0f;
        public bool CanSpendRoll => _config != null && _current >= _config.RollCost;

        public void Tick(float deltaTime)
        {
            if (_config == null)
            {
                return;
            }

            if (_regenDelayRemaining > 0f)
            {
                _regenDelayRemaining = Mathf.Max(0f, _regenDelayRemaining - deltaTime);
                return;
            }

            if (_current >= Max)
            {
                return;
            }

            SetCurrent(_current + _config.RegenPerSecond * deltaTime);
        }

        public void DrainSprint(float deltaTime)
        {
            if (_config == null)
            {
                return;
            }

            Spend(_config.SprintDrainPerSecond * deltaTime);
        }

        public bool TrySpendRoll()
        {
            if (!CanSpendRoll)
            {
                return false;
            }

            Spend(_config.RollCost);
            return true;
        }

        private void Spend(float amount)
        {
            _regenDelayRemaining = _config.RegenDelay;
            SetCurrent(_current - amount);
        }

        private void SetCurrent(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, Max);
            if (Mathf.Approximately(clamped, _current))
            {
                return;
            }

            _current = clamped;

            if (_current <= 0f)
            {
                _exhausted = true;
            }
            else if (_exhausted && _current >= _config.ExhaustedRecoveryThreshold)
            {
                _exhausted = false;
            }

            Changed?.Invoke(_current, Max);
        }
    }
}