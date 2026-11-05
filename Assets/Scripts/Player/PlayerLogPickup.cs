using System.Collections.Generic;
using ArkSurvival.Data;
using ArkSurvival.World;
using UnityEngine;

namespace ArkSurvival.Player
{
    /// <summary>
    /// Поиск ближайшего полена для подбора (DINO-046) и сам подбор по нажатию (DINO-047):
    /// перекрытие сферой радиуса <see cref="LogPickupConfig.PickupRange"/> по слоям
    /// <see cref="LogPickupConfig.LogLayerMask"/>. Пропускаются уже переносимые, учтённые
    /// хранилищем и уничтоженные поленья. Подбор сначала увеличивает счётчик
    /// <see cref="PlayerLogCarrier.AddLog"/> и только после успеха удаляет полено из мира.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerLogPickup : MonoBehaviour
    {
        private const int OverlapCapacity = 32;

        [Tooltip("Параметры подбора. Пусто — поиск поленьев отключается с предупреждением.")]
        [SerializeField] private LogPickupConfig _config;

        private readonly Collider[] _overlaps = new Collider[OverlapCapacity];
        private readonly Dictionary<Collider, LogItem> _logCache = new Dictionary<Collider, LogItem>();

        private PlayerController _player;
        private PlayerInputReader _input;
        private PlayerLogCarrier _carrier;

        private float _pickupLockUntil;

        /// <summary>Ближайшее доступное полено в радиусе; null, если такого нет.</summary>
        public LogItem CurrentTarget { get; private set; }

        /// <summary>Есть ли полено, которое можно подобрать.</summary>
        public bool HasTarget => CurrentTarget != null;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _input = GetComponent<PlayerInputReader>();
            _carrier = GetComponent<PlayerLogCarrier>();

            if (_config == null)
            {
                Debug.LogWarning(
                    "PlayerLogPickup: LogPickupConfig не назначен — поиск поленьев отключён. " +
                    "Назначьте ассет LogPickupConfig.",
                    this);
                enabled = false;
                return;
            }

            if (_carrier == null)
            {
                Debug.LogWarning(
                    "PlayerLogPickup: PlayerLogCarrier не найден — подбор поленьев отключён. " +
                    "Добавьте компонент PlayerLogCarrier на игрока.",
                    this);
                enabled = false;
                return;
            }

            _carrier.OnForcedDrop += HandleForcedDrop;
        }

        private void OnDestroy()
        {
            if (_carrier != null)
            {
                _carrier.OnForcedDrop -= HandleForcedDrop;
            }
        }

        private void HandleForcedDrop(int droppedCount)
        {
            if (_config == null)
            {
                return;
            }

            // После прыжка/кувырка поленья падают рядом: не подбираем их в том же движении.
            _pickupLockUntil = Time.time + _config.PickupLockAfterDrop;
        }

        private void Update()
        {
            if (_config == null)
            {
                // Конфиг мог быть снят в инспекторе после Awake: без него поиск недоступен.
                CurrentTarget = null;
                return;
            }

            if (_player != null && _player.IsDead)
            {
                // Мёртвый игрок ничего не подбирает и не показывает цель подбора.
                CurrentTarget = null;
                return;
            }

            if (Time.time < _pickupLockUntil)
            {
                // Только что принудительно сброшенные поленья ещё не подбираются.
                CurrentTarget = null;
                return;
            }

            CurrentTarget = FindTarget();

            if (_input == null || !_input.PickupPressed)
            {
                return;
            }

            TryPickUp();
        }

        private void TryPickUp()
        {
            if (_player != null && _player.IsDead)
            {
                return;
            }

            if (CurrentTarget == null || !_carrier.CanPickUp)
            {
                return;
            }

            LogItem target = CurrentTarget;

            // Сначала счётчик: при отказе (лимит) полено остаётся в мире нетронутым.
            if (!_carrier.AddLog())
            {
                return;
            }

            CurrentTarget = null;
            target.DestroyLog();
        }

        private LogItem FindTarget()
        {
            Vector3 origin = transform.position;
            float range = _config.PickupRange;

            int count = Physics.OverlapSphereNonAlloc(
                origin,
                range,
                _overlaps,
                _config.LogLayerMask,
                QueryTriggerInteraction.Ignore);

            LogItem nearest = null;
            float nearestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                LogItem log = ResolveLog(collider);
                if (!IsAvailable(log))
                {
                    continue;
                }

                Vector3 delta = collider.ClosestPoint(origin) - origin;
                float sqrDistance = delta.sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = log;
                }
            }

            return nearest;
        }

        private static bool IsAvailable(LogItem log)
        {
            // Уничтоженный объект в Unity сравнивается с null.
            return log != null && !log.IsDestroyed && !log.IsCarried && !log.IsRegistered;
        }

        private LogItem ResolveLog(Collider collider)
        {
            if (_logCache.TryGetValue(collider, out LogItem cached))
            {
                if (cached != null)
                {
                    return cached;
                }

                _logCache.Remove(collider);
            }

            LogItem log = collider.GetComponentInParent<LogItem>();
            if (log != null)
            {
                _logCache[collider] = log;
            }

            return log;
        }
    }
}
