using UnityEngine;
using UnityEngine.AI;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Выбор точки патруля: случайная точка в радиусе <c>CreatureConfig.PatrolRadius</c> вокруг
    /// текущей домашней точки, привязанная к NavMesh и достижимая полным путём. Точки слишком
    /// близко к текущей позиции отбрасываются, число попыток ограничено. Только выбирает точку:
    /// движение, скорость и паузы — в <see cref="CreatureStates.PatrolState"/> (DINO-018).
    /// </summary>
    public sealed class CreaturePatrolPointSelector
    {
        /// <summary>Радиус привязки случайной точки к NavMesh, м.</summary>
        private const float PointSampleRadius = 2f;

        /// <summary>Минимальное расстояние от текущей позиции до новой точки, м (временное).</summary>
        private const float MinPointDistance = 2f;

        /// <summary>Сколько попыток подобрать достижимую точку за один выбор (временное).</summary>
        private const int MaxAttempts = 10;

        private const float DefaultRadius = 10f;

        private readonly CreatureContext _context;

        public CreaturePatrolPointSelector(CreatureContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Пытается выбрать достижимую точку патруля. Возвращает false, если агент не готов,
        /// радиус не задан или за <see cref="MaxAttempts"/> попыток точка не найдена.
        /// </summary>
        public bool TrySelectPoint(out Vector3 point)
        {
            point = Vector3.zero;

            if (_context == null)
            {
                return false;
            }

            CreatureNavigator navigator = _context.Navigator;
            if (navigator == null || !navigator.IsReady)
            {
                return false;
            }

            float radius = GetRadius();
            if (radius <= 0f)
            {
                return false;
            }

            Vector3 center = _context.HomePosition;
            Vector3 current = navigator.Position;
            float minDistanceSqr = MinPointDistance * MinPointDistance;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

                if (HorizontalSqrDistance(candidate, current) < minDistanceSqr)
                {
                    continue;
                }

                if (!TrySnapToNavMesh(navigator.Agent, candidate, out Vector3 snapped))
                {
                    continue;
                }

                // Привязка к NavMesh могла сдвинуть точку ближе к существу, чем допустимо.
                if (HorizontalSqrDistance(snapped, current) < minDistanceSqr)
                {
                    continue;
                }

                if (!navigator.CanReach(snapped, PointSampleRadius))
                {
                    continue;
                }

                point = snapped;
                return true;
            }

            return false;
        }

        private float GetRadius()
        {
            return _context.Config != null ? Mathf.Max(0f, _context.Config.PatrolRadius) : DefaultRadius;
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        private static bool TrySnapToNavMesh(NavMeshAgent agent, Vector3 position, out Vector3 result)
        {
            result = position;
            if (agent == null)
            {
                return false;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, PointSampleRadius, filter))
            {
                return false;
            }

            result = hit.position;
            return true;
        }
    }
}
