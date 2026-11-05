using System;
using ArkSurvival.Data;
using ArkSurvival.World;
using UnityEngine;

namespace ArkSurvival.Player
{
    /// <summary>
    /// Счётчик переносимых игроком поленьев (DINO-044, DINO-045): число, API изменения
    /// и лимит из <see cref="LogCarryConfig.MaxCarriedLogs"/>. Ввод и визуал появятся
    /// в следующих задачах; здесь их нет.
    /// </summary>
    public sealed class PlayerLogCarrier : MonoBehaviour
    {
        private const int FallbackMaxCarriedLogs = 3;

        [Tooltip("Параметры переноски. Пусто — используется безопасный лимит по умолчанию.")]
        [SerializeField] private LogCarryConfig _config;

        [Tooltip("Визуальные слоты переноски: откуда берутся точки появления. Пусто — сброс через DropAll отключается.")]
        [SerializeField] private CarriedLogsVisual _visuals;

        [Tooltip("Префаб полена с компонентом LogItem, Rigidbody и Collider. Пусто — DropAll не сбрасывает поленья.")]
        [SerializeField] private LogItem _logPrefab;

        [Tooltip("Импульс разлёта поленьев при DropAll, м/с (временное).")]
        [SerializeField] private float _dropImpulse = 1.5f;

        [Tooltip("Доля импульса вверх, чтобы поленья не скользили по земле (временное).")]
        [SerializeField] private float _dropUpwardShare = 0.4f;

        private int _count;

        /// <summary>Сколько поленьев сейчас переносит игрок; всегда 0..<see cref="MaxCarriedLogs"/>.</summary>
        public int Count => _count;

        /// <summary>Лимит переносимых поленьев из конфига; без конфига — безопасное значение.</summary>
        public int MaxCarriedLogs =>
            _config != null ? _config.MaxCarriedLogs : FallbackMaxCarriedLogs;

        /// <summary>Можно ли взять ещё одно полено: лимит не достигнут.</summary>
        public bool CanPickUp => _count < MaxCarriedLogs;

        /// <summary>Переносит ли игрок хотя бы одно полено.</summary>
        public bool IsCarrying => _count > 0;

        /// <summary>Новое количество поленьев после успешного изменения.</summary>
        public event Action<int> OnCountChanged;

        /// <summary>
        /// Изменилось ли состояние переноски. Вызывается только при переходе
        /// <c>0 ↔ &gt;0</c>, а не на каждое изменение количества.
        /// </summary>
        public event Action<bool> OnCarryStateChanged;

        /// <summary>
        /// Поленья принудительно сброшены (прыжок, кувырок). Аргумент — сколько поленьев
        /// выпало. Ручной сброс через <see cref="PlayerLogDrop"/> это событие не вызывает.
        /// </summary>
        public event Action<int> OnForcedDrop;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogWarning(
                    "PlayerLogCarrier: LogCarryConfig не назначен — лимит " +
                    FallbackMaxCarriedLogs + " по умолчанию. Назначьте ассет LogCarryConfig.",
                    this);
            }
        }

        /// <summary>
        /// Добавляет одно полено и сообщает новое количество. На лимите отказ:
        /// счётчик и событие не меняются, возвращается <c>false</c>.
        /// </summary>
        public bool AddLog()
        {
            if (!CanPickUp)
            {
                return false;
            }

            _count++;
            OnCountChanged?.Invoke(_count);

            if (_count == 1)
            {
                OnCarryStateChanged?.Invoke(true);
            }

            return true;
        }

        /// <summary>
        /// Убирает одно полено. Если поленьев нет, счётчик остаётся нулевым,
        /// событие не вызывается и возвращается <c>false</c>.
        /// </summary>
        public bool RemoveLog()
        {
            if (_count <= 0)
            {
                return false;
            }

            _count--;
            OnCountChanged?.Invoke(_count);

            if (_count == 0)
            {
                OnCarryStateChanged?.Invoke(false);
            }

            return true;
        }

        /// <summary>
        /// Сбрасывает все переносимые поленья (DINO-060): каждое появляется из префаба
        /// в точке своего визуального слота <see cref="CarriedLogsVisual"/> с небольшим
        /// импульсом в случайном направлении, после чего счётчик обнуляется. Нет префаба
        /// или визуалов — предупреждение, счётчик не меняется и поленья не теряются.
        /// </summary>
        public void DropAll(DropReason reason)
        {
            if (_count <= 0)
            {
                return;
            }

            if (_logPrefab == null)
            {
                Debug.LogWarning(
                    "PlayerLogCarrier: префаб полена не назначен — DropAll(" + reason + ") пропущен, " +
                    "поленья остаются у игрока. Назначьте префаб с компонентом LogItem.",
                    this);
                return;
            }

            if (_visuals == null)
            {
                Debug.LogWarning(
                    "PlayerLogCarrier: CarriedLogsVisual не назначен — DropAll(" + reason + ") пропущен, " +
                    "поленья остаются у игрока. Назначьте CarriedLogsVisual в поле Visuals.",
                    this);
                return;
            }

            int droppedCount = _count;
            float impulse = Mathf.Max(0f, _dropImpulse);
            float upward = impulse * Mathf.Max(0f, _dropUpwardShare);

            for (int i = 0; i < droppedCount; i++)
            {
                Transform slot = _visuals.GetSlot(i);
                Vector3 position = slot != null ? slot.position : transform.position;
                Quaternion rotation = slot != null ? slot.rotation : transform.rotation;

                LogItem instance = Instantiate(_logPrefab, position, rotation);

                if (instance.TryGetComponent(out Rigidbody body))
                {
                    Vector2 direction = UnityEngine.Random.insideUnitCircle; 
                    if (direction.sqrMagnitude < 0.0001f)
                    {
                        direction = Vector2.right;
                    }

                    direction.Normalize();
                    Vector3 force = new Vector3(direction.x, 0f, direction.y) * impulse
                        + Vector3.up * upward;
                    body.AddForce(force, ForceMode.VelocityChange);
                }
            }

            _count = 0;
            OnCountChanged?.Invoke(_count);
            OnCarryStateChanged?.Invoke(false);

            // Только принудительный сброс (прыжок/кувырок) блокирует подбор: ручной сброс
            // в <see cref="PlayerLogDrop"/> идёт другим путём.
            if (reason == DropReason.Jump || reason == DropReason.Roll)
            {
                OnForcedDrop?.Invoke(droppedCount);
            }
        }
    }
}
