using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Контейнер зависимостей существа, передаваемый в состояния.
    /// Содержит навигационный адаптер и исходную точку; остальные компоненты добавятся по мере задач.
    /// </summary>
    public sealed class CreatureContext
    {
        public CreatureNavigator Navigator { get; }
        public Vector3 HomePosition { get; }

        public CreatureContext(CreatureNavigator navigator, Vector3 homePosition)
        {
            Navigator = navigator;
            HomePosition = homePosition;
        }
    }
}
