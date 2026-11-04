using ArkSurvival.AI.Perception;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Временная ручная проверка слуха (DINO-020): по клавише N публикует шум заданной громкости
    /// из указанной точки. Нужен только для проверки в Play Mode, пока игрок не издаёт шум сам
    /// (DINO-022). После DINO-022 компонент можно удалить.
    /// </summary>
    public sealed class CreatureNoiseDebugInput : MonoBehaviour
    {
        [Tooltip("Источник шума. Пусто — позиция этого объекта.")]
        [SerializeField] private Transform _source;

        [Tooltip("Громкость шума 0..1 для ручной проверки.")]
        [SerializeField, Range(0f, 1f)] private float _loudness = 1f;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.nKey.wasPressedThisFrame)
            {
                return;
            }

            Transform source = _source != null ? _source : transform;
            NoiseBroadcaster.Emit(source.position, _loudness, source);
            Debug.Log($"CreatureNoiseDebugInput: отправлен шум из «{source.name}», громкость {_loudness:F2}.", this);
        }
    }
}
