using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Контейнер зависимостей существа, передаваемый в состояния.
    /// Содержит навигационный адаптер, домашнюю точку, память о цели, зрение, тик восприятия и конфиг;
    /// остальные компоненты добавятся по мере задач.
    /// </summary>
    public sealed class CreatureContext
    {
        public CreatureNavigator Navigator { get; }
        public CreatureTargetMemory Memory { get; }
        public CreatureVision Vision { get; }
        public CreaturePerception Perception { get; }
        public CreatureConfig Config { get; }
        public CreatureHome Home { get; }

        /// <summary>Приём шума и эффективная дальность (DINO-020). Переходы по шуму — DINO-021.</summary>
        public CreatureHearing Hearing { get; }

        /// <summary>Выбор случайной достижимой точки патруля вокруг домашней точки (DINO-017).</summary>
        public CreaturePatrolPointSelector PatrolPoints { get; }

        /// <summary>Текущая домашняя точка. Читается из <see cref="Home"/>, поэтому смена точки видна всем состояниям.</summary>
        public Vector3 HomePosition => Home != null ? Home.Position : Navigator != null ? Navigator.Position : Vector3.zero;

        public CreatureContext(
            CreatureNavigator navigator,
            CreatureTargetMemory memory,
            CreatureVision vision,
            CreaturePerception perception,
            CreatureHearing hearing,
            CreatureConfig config,
            CreatureHome home)
        {
            Navigator = navigator;
            Memory = memory;
            Vision = vision;
            Perception = perception;
            Hearing = hearing;
            Config = config;
            Home = home;
            PatrolPoints = new CreaturePatrolPointSelector(this);
        }
    }
}
