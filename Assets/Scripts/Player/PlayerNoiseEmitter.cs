using ArkSurvival.AI.Perception;
using UnityEngine;

namespace ArkSurvival.Player
{
    /// <summary>
    /// Отправка шума от действий игрока (DINO-022): бег даёт шаги через интервал, приземление после
    /// падения — один шум. Компонент необязательный и самодостаточный: он только читает публичное
    /// состояние <see cref="PlayerController"/>/<see cref="CharacterMotor"/> и публикует события
    /// в <see cref="NoiseBroadcaster"/>. Правила приоритета шума и зрения — DINO-023.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerNoiseEmitter : MonoBehaviour
    {
        [Header("Sprint")]
        [Tooltip("Громкость шума шага при беге, 0..1 (временное).")]
        [SerializeField, Range(0f, 1f)] private float _sprintLoudness = 0.7f;

        [Tooltip("Интервал между шагами при беге, с (временное).")]
        [SerializeField] private float _sprintStepInterval = 0.35f;

        [Header("Landing")]
        [Tooltip("Громкость шума приземления, 0..1 (временное).")]
        [SerializeField, Range(0f, 1f)] private float _landingLoudness = 1f;

        [Tooltip("Минимальная скорость падения для шума приземления, м/с (временное).")]
        [SerializeField] private float _landingMinFallSpeed = 3f;

        private PlayerController _player;
        private CharacterMotor _motor;
        private float _sprintStepTimer;
        private bool _wasGrounded;
        private float _lastAirborneFallSpeed;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _motor = GetComponent<CharacterMotor>();
            _wasGrounded = _motor != null && _motor.IsGrounded;
        }

        private void Update()
        {
            if (_player == null || _motor == null || _player.StateMachine == null)
            {
                return;
            }

            // Мёртвый игрок не шумит (DINO-028): иначе «замороженное» состояние Sprint
            // продолжало бы издавать шаги и уводило существо в Investigate.
            if (_player.IsDead)
            {
                _sprintStepTimer = 0f;
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateLanding();
            UpdateSprint(deltaTime);
        }

        /// <summary>Один шум в момент касания земли после падения достаточно быстрым падением.</summary>
        private void UpdateLanding()
        {
            bool grounded = _motor.IsGrounded;
            if (grounded && !_wasGrounded && _lastAirborneFallSpeed >= _landingMinFallSpeed)
            {
                Emit(_landingLoudness);
            }

            if (!grounded)
            {
                _lastAirborneFallSpeed = Mathf.Max(0f, -_motor.VerticalVelocity);
            }

            _wasGrounded = grounded;
        }

        /// <summary>Шаги бега: шум повторяется через интервал, пока игрок в состоянии Sprint.</summary>
        private void UpdateSprint(float deltaTime)
        {
            if (_player.StateMachine.Current != _player.StateMachine.Sprint)
            {
                _sprintStepTimer = 0f;
                return;
            }

            if (_sprintStepTimer > 0f)
            {
                _sprintStepTimer = Mathf.Max(0f, _sprintStepTimer - deltaTime);
                if (_sprintStepTimer > 0f)
                {
                    return;
                }
            }

            _sprintStepTimer = Mathf.Max(0.05f, _sprintStepInterval);
            Emit(_sprintLoudness);
        }

        private void Emit(float loudness)
        {
            NoiseBroadcaster.Emit(transform.position, loudness, transform);
        }
    }
}
