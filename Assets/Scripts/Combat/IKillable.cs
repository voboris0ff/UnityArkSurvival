using UnityEngine;

namespace ArkSurvival.Combat
{
    /// <summary>
    /// Цель, которую можно убить одним попаданием (DINO-024/DINO-028).
    /// Системы здоровья нет: попадание смертельно, если цель не мертва и не неуязвима.
    /// </summary>
    public interface IKillable
    {
        /// <summary>Цель уже мертва: повторно атаковать её не нужно.</summary>
        bool IsDead { get; }

        /// <summary>
        /// Попытка убить цель. <paramref name="source"/> — позиция атакующего.
        /// <c>true</c> — попадание принято, цель мертва; <c>false</c> — цель неуязвима
        /// (i-frames) или уже мертва, повторять попытку в этом замахе нельзя.
        /// </summary>
        bool TryKill(Vector3 source);
    }
}
