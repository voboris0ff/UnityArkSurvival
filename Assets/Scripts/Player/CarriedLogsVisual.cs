using UnityEngine;

namespace ArkSurvival.Player
{
    /// <summary>
    /// Визуал переносимых поленьев (DINO-050): фиксированные слоты до
    /// <see cref="PlayerLogCarrier.MaxCarriedLogs"/> создаются в <c>Awake</c> под якорем,
    /// показываются по <see cref="PlayerLogCarrier.OnCountChanged"/> и раскладываются
    /// со смещением на слот. Коллайдеры и физика визуалов отключаются; нет якоря или
    /// префаба — предупреждение и никаких визуалов, без исключений.
    /// </summary>
    [RequireComponent(typeof(PlayerLogCarrier))]
    public sealed class CarriedLogsVisual : MonoBehaviour
    {
        [Tooltip("Якорь переноски: родитель визуалов (обычно дочерний объект игрока). Пусто — визуалы отключены.")]
        [SerializeField] private Transform _anchor;

        [Tooltip("Визуальный префаб полена без физики на первом слоте.")]
        [SerializeField] private GameObject _visualPrefab;

        [Tooltip("Смещение первого слота относительно якоря.")]
        [SerializeField] private Vector3 _firstOffset = new Vector3(0f, 0f, 0.25f);

        [Tooltip("Прибавка смещения на каждый следующий слот (укладка).")]
        [SerializeField] private Vector3 _stackStep = new Vector3(0f, 0.12f, 0f);

        [Tooltip("Локальный поворот визуала каждого слота.")]
        [SerializeField] private Vector3 _localEulerAngles = new Vector3(0f, 90f, 0f);

        private PlayerLogCarrier _carrier;
        private GameObject[] _slots;

        private void Awake()
        {
            _carrier = GetComponent<PlayerLogCarrier>();

            if (_anchor == null)
            {
                Debug.LogWarning(
                    "CarriedLogsVisual: якорь переноски не назначен — визуалы поленьев отключены. " +
                    "Добавьте дочерний объект-якорь и назначьте его в поле Anchor.",
                    this);
                enabled = false;
                return;
            }

            if (_visualPrefab == null)
            {
                Debug.LogWarning(
                    "CarriedLogsVisual: визуальный префаб полена не назначен — визуалы отключены. " +
                    "Назначьте префаб в поле Visual Prefab.",
                    this);
                enabled = false;
                return;
            }

            CreateSlots();
        }

        private void OnEnable()
        {
            if (_carrier == null)
            {
                return;
            }

            _carrier.OnCountChanged += HandleCountChanged;
            Refresh(_carrier.Count);
        }

        private void OnDisable()
        {
            if (_carrier != null)
            {
                _carrier.OnCountChanged -= HandleCountChanged;
            }
        }

        private void HandleCountChanged(int count)
        {
            Refresh(count);
        }

        /// <summary>
        /// Трансформ слота визуала по индексу (DINO-060). <c>null</c>, если визуалы отключены
        /// (нет якоря или префаба) либо индекс вне диапазона.
        /// </summary>
        public Transform GetSlot(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length)
            {
                return null;
            }

            GameObject slot = _slots[index];
            return slot != null ? slot.transform : null;
        }

        private void CreateSlots()
        {
            int capacity = Mathf.Max(1, _carrier.MaxCarriedLogs);
            _slots = new GameObject[capacity];

            for (int i = 0; i < capacity; i++)
            {
                GameObject slot = Instantiate(_visualPrefab, _anchor, false);
                PrepareSlot(slot, i);
                _slots[i] = slot;
            }
        }

        private void PrepareSlot(GameObject slot, int index)
        {
            slot.name = _visualPrefab.name + " (Carried " + index + ")";
            slot.transform.SetLocalPositionAndRotation(
                _firstOffset + _stackStep * index,
                Quaternion.Euler(_localEulerAngles));

            // Визуал не участвует в физике и подборе: ни коллайдеров, ни гравитации.
            Collider[] colliders = slot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] bodies = slot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].detectCollisions = false;
                bodies[i].useGravity = false;
                bodies[i].isKinematic = true;
            }

            slot.SetActive(false);
        }

        private void Refresh(int count)
        {
            if (_slots == null)
            {
                return;
            }

            int visibleCount = Mathf.Clamp(count, 0, _slots.Length);

            for (int i = 0; i < _slots.Length; i++)
            {
                GameObject slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                bool shouldShow = i < visibleCount;
                if (slot.activeSelf != shouldShow)
                {
                    slot.SetActive(shouldShow);
                }
            }
        }
    }
}
