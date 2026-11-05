using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.Player
{
    public sealed class PlayerCameraCollision
    {
        private const float Skin = 0.02f;

        private static readonly RaycastHit[] SphereHits = new RaycastHit[8];
        private static readonly RaycastHit[] RayHits = new RaycastHit[8];

        private float _currentDistance = -1f;

        // Keeps the orbit pivot (the player's look point shifted to the shoulder) out of
        // geometry: with the shoulder offset the pivot can end up inside a wall the player
        // stands next to, and the camera would look through that wall.
        public Vector3 ResolvePivot(
            Vector3 pivot,
            Vector3 desiredPivot,
            PlayerCameraConfig config,
            Transform target,
            float nearClipPlane)
        {
            Vector3 offset = desiredPivot - pivot;
            float desiredDistance = offset.magnitude;

            if (desiredDistance <= 0.0001f || config.CollisionLayerMask == 0)
            {
                return desiredPivot;
            }

            Vector3 direction = offset / desiredDistance;
            float radius = Mathf.Max(0f, config.CollisionRadius);
            float needed = nearClipPlane + Skin;
            float allowedDistance = desiredDistance;

            if (radius > 0f)
            {
                float sphereDistance = FindNearestHit(
                    Physics.SphereCastNonAlloc(
                        pivot,
                        radius,
                        direction,
                        SphereHits,
                        desiredDistance,
                        config.CollisionLayerMask,
                        QueryTriggerInteraction.Ignore),
                    SphereHits,
                    desiredDistance,
                    target);

                allowedDistance = Mathf.Min(allowedDistance, sphereDistance + radius - needed);
            }

            float rayDistance = FindNearestHit(
                Physics.RaycastNonAlloc(
                    pivot,
                    direction,
                    RayHits,
                    desiredDistance,
                    config.CollisionLayerMask,
                    QueryTriggerInteraction.Ignore),
                RayHits,
                desiredDistance,
                target);

            allowedDistance = Mathf.Min(allowedDistance, rayDistance - needed);

            return pivot + direction * Mathf.Clamp(allowedDistance, 0f, desiredDistance);
        }

        // nearClipPlane is the camera's near clip distance: the camera never goes closer to
        // geometry than that, otherwise the wall gets clipped and the view sees through it.
        public Vector3 Resolve(
            Vector3 pivot,
            Vector3 desiredPosition,
            PlayerCameraConfig config,
            Transform target,
            float nearClipPlane,
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
            float allowedDistance = FindAllowedDistance(
                pivot,
                direction,
                desiredDistance,
                config,
                target,
                nearClipPlane);
            float targetDistance = allowedDistance < desiredDistance
                ? Mathf.Max(allowedDistance, 0f)
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
            Transform target,
            float nearClipPlane)
        {
            // A sphere hit reports the point where the sphere surface touches the wall, so the
            // camera ends up CollisionRadius away from it. If that is less than the near clip
            // distance, the wall is inside the near plane and the view sees through it.
            float sphereExtra = Mathf.Max(0f, nearClipPlane + Skin - config.CollisionRadius);
            float sphereDistance = FindNearestHit(
                Physics.SphereCastNonAlloc(
                    pivot,
                    config.CollisionRadius,
                    direction,
                    SphereHits,
                    desiredDistance,
                    config.CollisionLayerMask,
                    QueryTriggerInteraction.Ignore),
                SphereHits,
                desiredDistance,
                target);

            // A sphere that already overlaps a collider at the start is ignored by SphereCast,
            // which is exactly the case when the player stands close to a wall. A thin ray from
            // the pivot still reports that wall and keeps the camera out of it.
            float rayExtra = nearClipPlane + Skin;
            float rayDistance = FindNearestHit(
                Physics.RaycastNonAlloc(
                    pivot,
                    direction,
                    RayHits,
                    desiredDistance,
                    config.CollisionLayerMask,
                    QueryTriggerInteraction.Ignore),
                RayHits,
                desiredDistance,
                target);

            float allowedDistance = Mathf.Min(
                sphereDistance - sphereExtra,
                rayDistance - rayExtra);

            return Mathf.Clamp(allowedDistance, 0f, desiredDistance);
        }

        private static float FindNearestHit(
            int count,
            RaycastHit[] hits,
            float desiredDistance,
            Transform target)
        {
            float nearest = desiredDistance;

            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || IsTarget(collider.transform, target))
                {
                    continue;
                }

                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                }
            }

            return nearest;
        }

        private static bool IsTarget(Transform candidate, Transform target)
        {
            return target != null && (candidate == target || candidate.IsChildOf(target));
        }
    }
}
