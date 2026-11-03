using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Проверка зрения существа: дистанция, угол обзора и прямая видимость до цели.
    /// Только отвечает на вопрос «вижу ли цель прямо сейчас» — не помнит её, не принимает решений
    /// о переходах состояний и не имеет собственного таймера (тик восприятия — DINO-010).
    /// </summary>
    public sealed class CreatureVision
    {
        private const float HitBuffer = 0.5f;
        private static readonly RaycastHit[] TargetProbeHits = new RaycastHit[16];

        private readonly Transform _eye;
        private readonly float _sightDistance;
        private readonly float _sightAngle;
        private readonly LayerMask _obstacleLayerMask;

        /// <param name="eye">Точка, из которой существо смотрит (обычно Transform на высоте глаз).</param>
        /// <param name="config">Источник дистанции, угла и слоя препятствий.</param>
        public CreatureVision(Transform eye, CreatureConfig config)
        {
            _eye = eye;
            _sightDistance = config != null ? Mathf.Max(0f, config.SightDistance) : 0f;
            _sightAngle = config != null ? Mathf.Clamp(config.SightAngle, 0f, 360f) : 0f;
            _obstacleLayerMask = config != null ? config.ObstacleLayerMask : 0;
        }

        /// <summary>Дистанция обзора из конфига, м.</summary>
        public float SightDistance => _sightDistance;

        /// <summary>Полный угол обзора из конфига, градусы.</summary>
        public float SightAngle => _sightAngle;

        /// <summary>Слой препятствий, перекрывающих линию взгляда.</summary>
        public LayerMask ObstacleLayerMask => _obstacleLayerMask;

        /// <summary>Видна ли цель прямо сейчас.</summary>
        public bool CanSee(Transform target)
        {
            return CanSee(target, out _);
        }

        /// <summary>
        /// Проверяет видимость цели. При успехе возвращает точку, которую существо реально видит:
        /// ближайшую к глазам точку коллайдера цели (обычно грудь/корпус), а не её pivot у ног.
        /// Цель без коллайдеров не видна — луч не за что зацепить.
        /// </summary>
        public bool CanSee(Transform target, out Vector3 visiblePoint)
        {
            visiblePoint = Vector3.zero;

            if (_eye == null || target == null)
            {
                return false;
            }

            if (!TryGetTargetPoint(target, out Vector3 aimPoint))
            {
                return false;
            }

            Vector3 eyePosition = _eye.position;
            Vector3 toTarget = aimPoint - eyePosition;
            float distance = toTarget.magnitude;
            if (distance > _sightDistance || distance <= Mathf.Epsilon)
            {
                return false;
            }

            if (!IsInsideViewAngle(eyePosition, aimPoint, distance))
            {
                return false;
            }

            Vector3 direction = toTarget / distance;
            if (!Physics.Raycast(
                    eyePosition,
                    direction,
                    out RaycastHit hit,
                    distance,
                    _obstacleLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                // Луч не упёрся ни в что (в том числе цель не в маске) — цель видна.
                visiblePoint = aimPoint;
                return true;
            }

            if (!hit.transform.IsChildOf(target))
            {
                return false;
            }

            // Ближайшим оказался коллайдер самой цели — она видна, луч немного не дотянул до центра.
            visiblePoint = hit.point;
            return true;
        }

        /// <summary>Только дистанция и угол обзора, без raycast.</summary>
        public bool IsInsideViewAngle(Vector3 from, Vector3 to, float distance)
        {
            if (_sightAngle >= 360f)
            {
                return true;
            }

            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                // Цель точно над/под существом: по горизонтали направления нет.
                return true;
            }

            Vector3 forward = _eye.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                return true;
            }

            float angle = Vector3.Angle(forward, direction);
            return angle <= _sightAngle * 0.5f;
        }

        /// <summary>Точка прицеливания: ближайший к глазам коллайдер цели, иначе центр её габаритов.</summary>
        private bool TryGetTargetPoint(Transform target, out Vector3 point)
        {
            point = Vector3.zero;

            // Цель могла быть уничтожена между вызовами — Transform превращается в «фальшивый null».
            if (target == null)
            {
                return false;
            }

            Vector3 toTarget = target.position - _eye.position;
            if (toTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                point = target.position;
                return true;
            }

            // NonAlloc: проверка повторяется каждый тик восприятия, мусор в GC не нужен.
            int count = Physics.RaycastNonAlloc(
                _eye.position,
                toTarget.normalized,
                TargetProbeHits,
                _sightDistance + HitBuffer,
                _obstacleLayerMask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = TargetProbeHits[i];
                if (candidate.distance < bestDistance && candidate.collider.transform.IsChildOf(target))
                {
                    bestDistance = candidate.distance;
                    point = candidate.point;
                }
            }

            if (bestDistance < float.MaxValue)
            {
                return true;
            }

            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.enabled)
            {
                point = renderer.bounds.center;
                return true;
            }

            return false;
        }
    }
}
