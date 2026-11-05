using ArkSurvival.World;
using UnityEngine;

namespace ArkSurvival.Player
{
    /// <summary>
    /// Сброс одного полена по нажатию (DINO-049, DINO-052): место приземления ищется через
    /// <see cref="LogDropPointFinder"/>, и только после успеха создаётся <see cref="LogItem"/>
    /// из префаба сбоку от игрока на уровне рук и получает импульс вперёд, после чего
    /// вызывается <see cref="PlayerLogCarrier.RemoveLog"/>. Нет точки, нет префаба или
    /// нет поленьев — ничего не меняется.
    /// </summary>
    [RequireComponent(typeof(PlayerLogCarrier))]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerLogDrop : MonoBehaviour
    {
        [Tooltip("Параметры поиска точки приземления. Пусто — сброс отключается с предупреждением.")]
        [SerializeField] private LogDropPointFinder _pointFinder;

        [Tooltip("Префаб полена с компонентом LogItem, Rigidbody и Collider. Пусто — сброс отключается.")]
        [SerializeField] private LogItem _logPrefab;

        [Tooltip("Высота появления полена над землёй, м — уровень рук (временное).")]
        [SerializeField] private float _handHeight = 1.2f;

        [Tooltip("Смещение точки появления вбок от игрока, м (временное).")]
        [SerializeField] private float _sideOffset = 0.5f;

        [Tooltip("Смещение точки появления вперёд от игрока, м (временное).")]
        [SerializeField] private float _forwardOffset = 0.2f;

        [Tooltip("Импульс вперёд при сбросе, м/с (временное).")]
        [SerializeField] private float _forwardImpulse = 2f;

        [Tooltip("Доля импульса вверх, чтобы полено не падало вертикально (временное).")]
        [SerializeField] private float _upwardImpulseShare = 0.2f;

        private PlayerLogCarrier _carrier;
        private PlayerController _player;
        private PlayerInputReader _input;

        private void Awake()
        {
            _carrier = GetComponent<PlayerLogCarrier>();
            _player = GetComponent<PlayerController>();
            _input = GetComponent<PlayerInputReader>();

            if (_pointFinder == null)
            {
                Debug.LogWarning(
                    "PlayerLogDrop: LogDropPointFinder не назначен — сброс поленьев отключён. " +
                    "Добавьте на игрока LogDropPointFinder и назначьте его в поле Point Finder.",
                    this);
                enabled = false;
                return;
            }

            if (_logPrefab == null)
            {
                Debug.LogWarning(
                    "PlayerLogDrop: префаб полена не назначен — сброс поленьев отключён. " +
                    "Назначьте префаб с компонентом LogItem.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_input == null || !_input.DropPressed)
            {
                return;
            }

            TryDrop();
        }

        private void TryDrop()
        {
            if (_player != null && _player.IsDead)
            {
                return;
            }

            if (_logPrefab == null || _carrier == null || _carrier.Count <= 0)
            {
                return;
            }

            if (_pointFinder == null)
            {
                return;
            }

            // Точка на земле подтверждает, что полену есть куда упасть.
            if (!_pointFinder.TryFindPoint(transform, out Vector3 groundPoint))
            {
                return;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            right.Normalize();

            // Появление сбоку на уровне рук: полено не рождается в земле и не проваливается.
            Vector3 spawnPosition = groundPoint
                + Vector3.up * Mathf.Max(0f, _handHeight)
                + right * _sideOffset
                + forward * _forwardOffset;

            LogItem instance = Instantiate(
                _logPrefab,
                spawnPosition,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

            if (instance.TryGetComponent(out Rigidbody body))
            {
                float impulse = Mathf.Max(0f, _forwardImpulse);
                Vector3 force = forward * impulse
                    + Vector3.up * (impulse * Mathf.Max(0f, _upwardImpulseShare));
                body.AddForce(force, ForceMode.VelocityChange);
            }

            if (!_carrier.RemoveLog())
            {
                // Счётчик изменился между проверкой и сбросом: не оставляем лишнее полено в мире.
                instance.DestroyLog();
            }
        }
    }
}
