using UnityEngine;

namespace ArkSurvival.World
{
    /// <summary>
    /// Поиск точки перед игроком для выпавшего полена (DINO-048): луч вниз до земли
    /// и проверка свободного места сферой. Перебираются основная дистанция и запасные;
    /// если ни одна точка не подошла, возвращается <c>false</c>.
    /// </summary>
    public sealed class LogDropPointFinder : MonoBehaviour
    {
        private const float DefaultForwardDistance = 1.2f;
        private const float DefaultProbeHeight = 2f;
        private const float DefaultProbeDepth = 4f;
        private const float DefaultClearanceRadius = 0.2f;
        private const float MinGroundNormalY = 0.5f;
        private const int OverlapCapacity = 8;
        private const int RaycastCapacity = 8;

        [Tooltip("Расстояние перед игроком для основной точки, м (временное).")]
        [SerializeField] private float _forwardDistance = DefaultForwardDistance;

        [Tooltip("Запасные расстояния перед игроком, м (временное).")]
        [SerializeField] private float[] _fallbackDistances = { 0.6f, 1.8f };

        [Tooltip("Высота начала луча над точкой, м (временное).")]
        [SerializeField] private float _probeHeight = DefaultProbeHeight;

        [Tooltip("Глубина луча ниже точки, м (временное).")]
        [SerializeField] private float _probeDepth = DefaultProbeDepth;

        [Tooltip("Радиус свободного места вокруг точки, м (временное).")]
        [SerializeField] private float _clearanceRadius = DefaultClearanceRadius;

        [Tooltip("Слои земли для луча вниз; пустая маска — все слои.")]
        [SerializeField] private LayerMask _groundLayerMask = ~0;

        [Tooltip("Слои, занятость которых запрещает точку: Player и Obstacle по умолчанию.")]
        [SerializeField] private LayerMask _blockingLayers = (1 << 6) | (1 << 7);

        private readonly Collider[] _overlaps = new Collider[OverlapCapacity];
        private readonly RaycastHit[] _hits = new RaycastHit[RaycastCapacity];

        /// <summary>
        /// Ищет свободную точку перед владельцем компонента и кладёт её в <paramref name="point"/>
        /// (на уровне земли). Возвращает <c>false</c>, если подходящего места нет.
        /// </summary>
        public bool TryFindPoint(out Vector3 point)
        {
            return TryFindPoint(transform, out point);
        }

        /// <summary>
        /// Ищет свободную точку перед <paramref name="player"/> и кладёт её в <paramref name="point"/>
        /// (на уровне земли). Возвращает <c>false</c>, если подходящего места нет.
        /// </summary>
        public bool TryFindPoint(Transform player, out Vector3 point)
        {
            point = default;

            if (player == null)
            {
                return false;
            }

            Vector3 forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            if (TryPointAt(player, player.position, forward, _forwardDistance, out point))
            {
                return true;
            }

            if (_fallbackDistances != null)
            {
                for (int i = 0; i < _fallbackDistances.Length; i++)
                {
                    if (TryPointAt(player, player.position, forward, _fallbackDistances[i], out point))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryPointAt(
            Transform player,
            Vector3 playerPosition,
            Vector3 forward,
            float distance,
            out Vector3 point)
        {
            point = default;

            float probeHeight = Mathf.Max(0f, _probeHeight);
            Vector3 candidate = playerPosition + forward * Mathf.Max(0f, distance);
            Vector3 origin = candidate + Vector3.up * probeHeight;

            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _hits,
                probeHeight + Mathf.Max(0f, _probeDepth),
                _groundLayerMask,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float nearestDistance = float.PositiveInfinity;
            RaycastHit nearest = default;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                Collider collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(player))
                {
                    continue;
                }

                if (hit.normal.y < MinGroundNormalY)
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearest = hit;
                    found = true;
                }
            }

            if (!found || !HasFreeSpace(player, nearest.collider, nearest.point))
            {
                return false;
            }

            point = nearest.point;
            return true;
        }

        private bool HasFreeSpace(Transform player, Collider ground, Vector3 groundPoint)
        {
            float radius = Mathf.Max(0f, _clearanceRadius);
            Vector3 center = groundPoint + Vector3.up * radius;

            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _overlaps,
                _blockingLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null || collider == ground)
                {
                    continue;
                }

                if (collider.transform.IsChildOf(player))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
