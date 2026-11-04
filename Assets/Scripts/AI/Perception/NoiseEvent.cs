using UnityEngine;

namespace ArkSurvival.AI.Perception
{
    /// <summary>
    /// Одно событие шума: где, насколько громко и кто издал звук.
    /// Контракт только описывает данные — не считает слышимость и не принимает решений
    /// (приём и дальность — DINO-020, связь с Investigate — DINO-021, отправка игроком — DINO-022).
    /// Структура, чтобы события шума не создавали мусор в GC.
    /// </summary>
    public readonly struct NoiseEvent
    {
        /// <param name="position">Точка в мире, где возник шум (не позиция слушателя).</param>
        /// <param name="loudness">
        /// Относительная громкость 0..1: 0 — на границе слышимости, 1 — источник рядом.
        /// Значение вне диапазона обрезается. Абсолютная дальность считается слухом (DINO-020).
        /// </param>
        /// <param name="source">
        /// Источник шума для идентификации (обычно корень игрока). Может быть null —
        /// тогда шум анонимный и слушатель не может отличить его от собственного.
        /// </param>
        public NoiseEvent(Vector3 position, float loudness, Transform source = null)
        {
            Position = position;
            Loudness = Mathf.Clamp01(loudness);
            Source = source;
        }

        /// <summary>Точка возникновения шума в мире.</summary>
        public Vector3 Position { get; }

        /// <summary>Относительная громкость 0..1 (1 — самый громкий источник).</summary>
        public float Loudness { get; }

        /// <summary>Источник шума для идентификации; может быть null.</summary>
        public Transform Source { get; }

        /// <summary>Есть ли у шума известный источник.</summary>
        public bool HasSource => Source != null;

        /// <summary>Имя источника для логов; не создаёт строку, пока лог не включён.</summary>
        public string SourceName => Source != null ? Source.name : "unknown";

        public override string ToString()
        {
            return $"NoiseEvent({Position}, loudness {Loudness:F2}, source {SourceName})";
        }
    }
}
