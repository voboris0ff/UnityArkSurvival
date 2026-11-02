using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerCameraCollision
    {
        private const float Skin = 0.02f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private float _currentDistance = -1f;

        public Vector3 Resolve(
            Vector3 pivot,
            Vector3 desiredPosition,
            PlayerCameraConfig config,
            Transform target,
            float deltaTime)
        {
            Vector3 offset = desiredPosition - pivot;
            float desiredDistance = offset.magnitude;

            if (desiredDistance <= 0.0001f)
            {
                _currentDistance = 0f;
                return desiredPosition;
            }

            if (config.CollisionRadius <= 0f || config.CollisionLayerMask == 0)
            {
                _currentDistance = desiredDistance;
                return desiredPosition;
            }

            Vector3 direction = offset / desiredDistance;
            float allowedDistance = FindAllowedDistance(pivot, direction, desiredDistance, config, target);
            float targetDistance = allowedDistance < desiredDistance
                ? Mathf.Max(allowedDistance - Skin, 0f)
                : desiredDistance;

            if (_currentDistance < 0f || targetDistance < _currentDistance)
            {
                _currentDistance = targetDistance;
            }
            else if (config.CollisionReturnSpeed > 0f)
            {
                _currentDistance = Mathf.MoveTowards(
                    _currentDistance,
                    targetDistance,
                    config.CollisionReturnSpeed * deltaTime);
            }
            else
            {
                _currentDistance = targetDistance;
            }

            float lowerBound = Mathf.Min(config.MinDistance, targetDistance);
            _currentDistance = Mathf.Clamp(_currentDistance, lowerBound, desiredDistance);

            return pivot + direction * _currentDistance;
        }

        private static float FindAllowedDistance(
            Vector3 pivot,
            Vector3 direction,
            float desiredDistance,
            PlayerCameraConfig config,
            Transform target)
        {
            int count = Physics.SphereCastNonAlloc(
                pivot,
                config.CollisionRadius,
                direction,
                Hits,
                desiredDistance,
                config.CollisionLayerMask,
                QueryTriggerInteraction.Ignore);

            float allowedDistance = desiredDistance;

            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider == null || IsTarget(collider.transform, target))
                {
                    continue;
                }

                if (Hits[i].distance < allowedDistance)
                {
                    allowedDistance = Hits[i].distance;
                }
            }

            return allowedDistance;
        }

        private static bool IsTarget(Transform candidate, Transform target)
        {
            return target != null && (candidate == target || candidate.IsChildOf(target));
        }
    }
}