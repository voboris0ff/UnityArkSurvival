using System;
using UnityEngine;

namespace ArkSurvival.AI.Perception
{
    /// <summary>
    /// Единственная точка публикации шумов: издатель вызывает <see cref="Emit"/>, слушатели
    /// подписываются на <see cref="Emitted"/> и сами решают, слышат ли они шум (DINO-020).
    /// Отдельной системы событий в проекте нет, поэтому контракт сделан минимальным.
    /// Статическое событие живёт всё время игры: слушатель обязан отписаться в OnDisable/OnDestroy,
    /// иначе уничтоженный объект останется в списке подписчиков.
    /// </summary>
    public static class NoiseBroadcaster
    {
        /// <summary>Подписка на все шумы. Вызывается синхронно в момент <see cref="Emit"/>.</summary>
        public static event Action<NoiseEvent> Emitted;

        /// <summary>
        /// При выключенном Domain Reload статики переживают выход из Play Mode: подписки прошлой
        /// сессии указывали бы на уничтоженные объекты. Поэтому список слушателей очищается при старте.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Emitted = null;
        }

        /// <summary>Публикует готовое событие шума.</summary>
        public static void Emit(NoiseEvent noise)
        {
            Emitted?.Invoke(noise);
        }

        /// <summary>Собирает событие шума из позиции и относительной громкости 0..1.</summary>
        public static void Emit(Vector3 position, float loudness, Transform source = null)
        {
            Emit(new NoiseEvent(position, loudness, source));
        }

        /// <summary>
        /// Сбрасывает всех подписчиков. Нужен только при выходе из Play Mode и в ручных проверках,
        /// чтобы отладочные подписки не переживали перезапуск сцены.
        /// </summary>
        public static void ClearListeners()
        {
            Emitted = null;
        }
    }
}
