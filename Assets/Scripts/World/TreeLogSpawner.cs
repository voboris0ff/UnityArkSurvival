using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.World
{
    /// <summary>
    /// Спавн поленьев из срубленного дерева (DINO-040): подписывается на
    /// <see cref="ChoppableTree.OnFelled"/>, создаёт <see cref="TreeConfig.LogsCount"/>
    /// <see cref="LogItem"/> в точках вокруг дерева в пределах <see cref="TreeConfig.LogSpawnRadius"/>
    /// на высоте <see cref="TreeConfig.LogSpawnHeight"/>
    /// и толкает их импульсом <see cref="TreeConfig.LogSpawnImpulse"/>. Точка считается свободной,
    /// если её не занимает чужой коллайдер; пропавший префаб полена даёт предупреждение без исключения.
    /// </summary>
    [RequireComponent(typeof(ChoppableTree))]
    public sealed class TreeLogSpawner : MonoBehaviour
    {
        private const int SpawnAttempts = 8;
        private const int OverlapCapacity = 8;

        /// <summary>Запас вокруг точки спавна для проверки свободного места, м (временное).</summary>
        private const float LogClearanceRadius = 0.15f;

        /// <summary>Доля импульса, уходящая вверх, чтобы поленья не скользили по земле (временное).</summary>
        private const float UpwardImpulseShare = 0.5f;

        [Tooltip("Конфигурация дерева с количеством и разлётом поленьев.")]
        [SerializeField] private TreeConfig _config;

        [Tooltip("Префаб полена с LogItem, Rigidbody и Collider. Пусто — спавн отключается.")]
        [SerializeField] private GameObject _logPrefab;

        [Tooltip("Слои, занятость которых запрещает спавн: Obstacle и Player по умолчанию.")]
        [SerializeField] private LayerMask _blockingLayers = (1 << 6) | (1 << 7);

        private readonly Collider[] _overlaps = new Collider[OverlapCapacity];
        private ChoppableTree _tree;

        private void Awake()
        {
            _tree = GetComponent<ChoppableTree>();

            if (_config == null)
            {
                Debug.LogWarning(
                    "TreeLogSpawner: TreeConfig не назначен — спавн поленьев отключён. " +
                    "Назначьте ассет TreeConfig.",
                    this);
            }

            if (_logPrefab == null)
            {
                Debug.LogWarning(
                    "TreeLogSpawner: префаб полена не назначен — спавн поленьев отключён. " +
                    "Назначьте префаб с компонентом LogItem.",
                    this);
            }

            if (_config == null || _logPrefab == null)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_tree == null)
            {
                return;
            }

            _tree.OnFelled += OnTreeFelled;
        }

        private void OnDisable()
        {
            if (_tree != null)
            {
                _tree.OnFelled -= OnTreeFelled;
            }
        }

        private void OnTreeFelled(ChoppableTree tree, Vector3 source)
        {
            // Защита от изменений в инспекторе после Awake: без префаба событие не ломает дерево.
            if (_config == null || _logPrefab == null)
            {
                return;
            }

            int count = Mathf.Max(0, _config.LogsCount);
            float radius = Mathf.Max(0f, _config.LogSpawnRadius);
            float impulse = Mathf.Max(0f, _config.LogSpawnImpulse);
            float height = _config.LogSpawnHeight;
            Vector3 origin = tree.transform.position;

            for (int i = 0; i < count; i++)
            {
                if (!TryFindSpawnPoint(tree, origin, radius, height, out Vector3 point))
                {
                    continue;
                }

                Vector3 outward = point - origin;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.0001f)
                {
                    outward = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
                }

                outward.Normalize();

                GameObject instance = Instantiate(_logPrefab, point, Random.rotation);
                if (instance.TryGetComponent(out Rigidbody body))
                {
                    Vector3 force = (outward + Vector3.up * UpwardImpulseShare) * impulse;
                    body.AddForce(force, ForceMode.VelocityChange);
                }
            }
        }

        private bool TryFindSpawnPoint(
            ChoppableTree tree,
            Vector3 origin,
            float radius,
            float height,
            out Vector3 point)
        {
            for (int attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = origin + new Vector3(offset.x, height, offset.y);

                if (IsFree(tree, candidate))
                {
                    point = candidate;
                    return true;
                }
            }

            point = origin;
            return false;
        }

        private bool IsFree(ChoppableTree tree, Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(
                point,
                LogClearanceRadius,
                _overlaps,
                _blockingLayers,
                QueryTriggerInteraction.Ignore);

            Transform treeTransform = tree.transform;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null || collider.transform.IsChildOf(treeTransform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
