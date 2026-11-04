using ArkSurvival.AI.Perception;
using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Приём шума существом: решает, слышен ли шум, по расстоянию и эффективной дальности
    /// (<c>HearingRange * Loudness</c>). Память и решения не трогает — хранит последнее
    /// услышанное событие и выдаёт его один раз через <see cref="TryConsumeHeardNoise"/>;
    /// переход в Investigate по шуму делает DINO-021.
    /// </summary>
    public sealed class CreatureHearing
    {
        private readonly Transform _listener;
        private readonly float _hearingRange;

        private bool _hasHeardNoise;
        private NoiseEvent _lastNoise;
        private float _lastDistance;

        // Событие, которое ещё не обработано машиной состояний: одно и то же услышанное
        // событие не должно запускать осмотр повторно каждый кадр.
        private bool _hasPendingNoise;
        private NoiseEvent _pendingNoise;

        /// <param name="listener">Точка слуха существа (обычно его Transform). Может быть null.</param>
        /// <param name="config">Источник дальности слуха.</param>
        public CreatureHearing(Transform listener, CreatureConfig config)
        {
            _listener = listener;
            _hearingRange = config != null ? Mathf.Max(0f, config.HearingRange) : 0f;
        }

        /// <summary>Максимальная дальность слуха из конфига, м.</summary>
        public float HearingRange => _hearingRange;

        /// <summary>Был ли услышан хотя бы один шум.</summary>
        public bool HasHeardNoise => _hasHeardNoise;

        /// <summary>Последнее услышанное событие шума.</summary>
        public NoiseEvent LastNoise => _lastNoise;

        /// <summary>Позиция последнего услышанного шума (приблизительная цель для Investigate).</summary>
        public Vector3 LastHeardPosition => _lastNoise.Position;

        /// <summary>Расстояние до последнего услышанного шума, м.</summary>
        public float LastDistance => _lastDistance;

        /// <summary>Эффективная дальность слышимости для громкости 0..1.</summary>
        public float EffectiveRange(float loudness)
        {
            return _hearingRange * Mathf.Clamp01(loudness);
        }

        /// <summary>
        /// Проверяет, слышно ли событие. Собственный шум (источник — сам слушатель или его потомок)
        /// игнорируется. При успехе событие запоминается как последнее услышанное.
        /// </summary>
        public bool Hear(NoiseEvent noise)
        {
            if (_listener == null)
            {
                return false;
            }

            if (noise.Source != null &&
                (noise.Source == _listener || noise.Source.IsChildOf(_listener)))
            {
                return false;
            }

            float range = EffectiveRange(noise.Loudness);
            if (range <= 0f)
            {
                return false;
            }

            float distance = Vector3.Distance(_listener.position, noise.Position);
            if (distance > range)
            {
                return false;
            }

            _hasHeardNoise = true;
            _lastNoise = noise;
            _lastDistance = distance;
            _hasPendingNoise = true;
            _pendingNoise = noise;
            return true;
        }

        /// <summary>
        /// Забирает услышанный шум, который ещё не был обработан. Возвращает true один раз
        /// на событие: машина состояний не запускает осмотр заново на том же шуме.
        /// </summary>
        public bool TryConsumeHeardNoise(out NoiseEvent noise)
        {
            if (!_hasPendingNoise)
            {
                noise = default;
                return false;
            }

            noise = _pendingNoise;
            _hasPendingNoise = false;
            _pendingNoise = default;
            return true;
        }

        /// <summary>Забыть последний услышанный шум и необработанное событие.</summary>
        public void Clear()
        {
            _hasHeardNoise = false;
            _lastNoise = default;
            _lastDistance = 0f;
            _hasPendingNoise = false;
            _pendingNoise = default;
        }
    }
}
