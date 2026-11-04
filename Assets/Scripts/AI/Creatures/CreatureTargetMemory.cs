using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Память существа о цели: кто был замечен, где и когда в последний раз.
    /// Только хранит данные и считает давность наблюдения — не принимает решений о переходах состояний
    /// и не выполняет проверок восприятия (это делают зрение/слух и состояния).
    /// </summary>
    public sealed class CreatureTargetMemory
    {
        private readonly float _memoryDuration;
        private Transform _target;
        private Vector3 _lastKnownPosition;
        private float _lastSeenTime;
        private bool _hasMemory;
        private float _elapsedTime;

        /// <param name="memoryDuration">Сколько секунд после последнего наблюдения информация о цели считается актуальной.</param>
        public CreatureTargetMemory(float memoryDuration)
        {
            _memoryDuration = Mathf.Max(0f, memoryDuration);
        }

        /// <summary>Сколько секунд память хранит цель после последнего наблюдения.</summary>
        public float MemoryDuration => _memoryDuration;

        /// <summary>Цель, которую видели последней. Живёт (не обнуляется) до <see cref="Clear"/>.</summary>
        public Transform Target => _target;

        /// <summary>Последняя известная позиция цели.</summary>
        public Vector3 LastKnownPosition => _lastKnownPosition;

        /// <summary>Время (<see cref="Time.time"/>) последнего наблюдения цели.</summary>
        public float LastSeenTime => _lastSeenTime;

        /// <summary>Есть ли в памяти цель, которую наблюдение ещё не истекло.</summary>
        public bool HasTarget => _hasMemory && IsWithinMemory();

        /// <summary>Число секунд с момента последнего наблюдения.</summary>
        public float TimeSinceLastSeen => _hasMemory ? Mathf.Max(0f, _elapsedTime) : 0f;

        /// <summary>
        /// Продвигать давность наблюдения на секунды, прошедшие за один кадр.
        /// Память не проверяет время сама: вызывающий код обязан тикать её каждый кадр,
        /// иначе запись никогда не протухнет и <see cref="HasTarget"/> останется true.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_hasMemory)
            {
                return;
            }

            _elapsedTime += Mathf.Max(0f, deltaTime);
        }

        /// <summary>Регистрирует наблюдение цели: запоминает её позицию и обнуляет давность.</summary>
        public void Observe(Transform target, Vector3 position)
        {
            _target = target;
            _lastKnownPosition = position;
            _lastSeenTime = Time.time;
            _elapsedTime = 0f;
            _hasMemory = true;
        }

        /// <summary>
        /// Запоминает приблизительную позицию (например, услышанный шум) как последнюю известную,
        /// не утверждая, что цель была видна: <see cref="Target"/> не меняется, давность отсчитывается заново.
        /// Благодаря этому шум даёт точку для Investigate, но не делает игрока видимой целью (DINO-021).
        /// </summary>
        public void RememberPoint(Vector3 position)
        {
            _lastKnownPosition = position;
            _lastSeenTime = Time.time;
            _elapsedTime = 0f;
            _hasMemory = true;
        }

        /// <summary>
        /// Обновляет позицию уже известной цели, не меняя время наблюдения.
        /// Возвращает false, если цель не та, что в памяти, или памяти нет — тогда время и позиция не трогаются.
        /// </summary>
        public bool UpdatePosition(Transform target, Vector3 position)
        {
            if (!_hasMemory || target == null || target != _target)
            {
                return false;
            }

            _lastKnownPosition = position;
            return true;
        }

        /// <summary>Полный сброс памяти: забыть цель, её позицию и время наблюдения.</summary>
        public void Clear()
        {
            _target = null;
            _lastKnownPosition = Vector3.zero;
            _lastSeenTime = 0f;
            _elapsedTime = 0f;
            _hasMemory = false;
        }

        private bool IsWithinMemory()
        {
            return _elapsedTime <= _memoryDuration;
        }
    }
}
