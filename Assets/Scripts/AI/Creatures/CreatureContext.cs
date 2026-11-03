using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Контейнер зависимостей существа, передаваемый в состояния.
    /// Содержит навигационный адаптер, исходную точку, память о цели, зрение, тик восприятия и конфиг;
    /// остальные компоненты добавятся по мере задач.
    /// </summary>
    public sealed class CreatureContext
    {
        public CreatureNavigator Navigator { get; }
        public CreatureTargetMemory Memory { get; }
        public CreatureVision Vision { get; }
        public CreaturePerception Perception { get; }
        public CreatureConfig Config { get; }
        public Vector3 HomePosition { get; }

        public CreatureContext(
            CreatureNavigator navigator,
            CreatureTargetMemory memory,
            CreatureVision vision,
            CreaturePerception perception,
            CreatureConfig config,
            Vector3 homePosition)
        {
            Navigator = navigator;
            Memory = memory;
            Vision = vision;
            Perception = perception;
            Config = config;
            HomePosition = homePosition;
        }
    }
}
