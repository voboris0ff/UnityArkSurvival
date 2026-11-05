namespace ArkSurvival.Player
{
    /// <summary>Причина сброса всех переносимых поленьев (DINO-060).</summary>
    public enum DropReason
    {
        /// <summary>Начало кувырка.</summary>
        Roll,

        /// <summary>Начало прыжка.</summary>
        Jump,

        /// <summary>Ручной сброс (например, по вводу игрока).</summary>
        Manual,
    }
}
