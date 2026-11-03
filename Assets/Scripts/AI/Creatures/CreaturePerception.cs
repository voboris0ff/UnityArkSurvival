using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Периодический тик восприятия: раз в <see cref="Interval"/> секунд проверяет зрение на текущей цели.
    /// Не выполняет Physics-проверки каждый кадр и не трогает память — только выдаёт результат
    /// последней проверки. Решения о переходах состояний принимает тот, кто вызывает <see cref="Tick"/>.
    /// </summary>
    public sealed class CreaturePerception
    {
        private readonly Transform _target;
        private readonly CreatureVision _vision;
        private readonly float _interval;
        private float _elapsedTime;

        private bool _hasResult;
        private bool _targetVisible;
        private Vector3 _visiblePoint;

        /// <param name="target">Цель для проверки (первая версия — игрок). Может быть null.</param>
        /// <param name="vision">Проверка зрения; без неё восприятие всегда возвращает «не вижу».</param>
        /// <param name="interval">Период между проверками, секунды.</param>
        public CreaturePerception(Transform target, CreatureVision vision, float interval)
        {
            _target = target;
            _vision = vision;
            _interval = Mathf.Max(0f, interval);
        }

        /// <summary>Период проверки из конфига, секунды.</summary>
        public float Interval => _interval;

        /// <summary>Была ли хотя бы одна проверка.</summary>
        public bool HasResult => _hasResult;

        /// <summary>Видна ли цель по результату последней проверки.</summary>
        public bool TargetVisible => _targetVisible;

        /// <summary>Точка, которую существо видело при последней успешной проверке.</summary>
        public Vector3 VisiblePoint => _visiblePoint;

        /// <summary>Цель, по которой идёт проверка.</summary>
        public Transform Target => _target;

        /// <summary>
        /// Вызывается каждый кадр, дорогая проверка — только по накопленному интервалу.
        /// При <c>Interval &lt;= 0</c> проверяет каждый кадр.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _elapsedTime += Mathf.Max(0f, deltaTime);
            if (!_hasResult || _elapsedTime >= _interval)
            {
                Refresh();
            }
        }

        /// <summary>Немедленная внеплановая проверка (например, при входе в состояние).</summary>
        public void Refresh()
        {
            _elapsedTime = 0f;
            _hasResult = true;
            Vector3 point = Vector3.zero;
            _targetVisible = _vision != null && _vision.CanSee(_target, out point);

            if (_targetVisible)
            {
                _visiblePoint = point;
            }
        }

        /// <summary>Забыть результат, например при смене цели или после уничтожения цели.</summary>
        public void Reset()
        {
            _elapsedTime = 0f;
            _hasResult = false;
            _targetVisible = false;
            _visiblePoint = Vector3.zero;
        }
    }
}
