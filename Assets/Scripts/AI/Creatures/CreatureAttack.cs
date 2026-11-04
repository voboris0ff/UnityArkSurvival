using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Ближняя атака существа (DINO-026): проверяет дистанцию и сектор цели, ведёт замах,
    /// в момент <c>CreatureConfig.AttackHitTime</c> один раз за замах решает, попала ли атака,
    /// и держит паузу <c>AttackCooldown</c> от конца замаха.
    /// Смерть цели здесь не применяется: попадание только фиксируется, потребитель в том же кадре
    /// забирает его через <see cref="TryConsumeHit"/> и вызывает <see cref="ArkSurvival.Combat.IKillable"/>
    /// (DINO-028). Повторной проверки в одном замахе нет ни при промахе, ни при отказе <c>TryKill</c>.
    /// </summary>
    public sealed class CreatureAttack
    {
        /// <summary>Сектор атаки считается по горизонтали; по высоте допустим небольшой разброс, м.</summary>
        private const float VerticalTolerance = 1.5f;

        private readonly Transform _self;
        private readonly float _range;
        private readonly float _halfAngle;
        private readonly float _windup;
        private readonly float _hitTime;
        private readonly float _cooldown;

        private float _windupElapsed;
        private float _cooldownRemaining;
        private Transform _target;
        private Transform _hitTarget;
        private bool _hitChecked;
        private bool _hitLanded;

        public CreatureAttack(Transform self, CreatureConfig config)
        {
            _self = self;
            _range = config != null ? Mathf.Max(0f, config.AttackRange) : 0f;
            float angle = config != null ? Mathf.Clamp(config.AttackAngle, 0f, 360f) : 0f;
            _halfAngle = angle * 0.5f;
            _windup = config != null ? Mathf.Max(0f, config.AttackWindup) : 0f;
            _hitTime = config != null ? Mathf.Clamp(config.AttackHitTime, 0f, _windup) : 0f;
            _cooldown = config != null ? Mathf.Max(0f, config.AttackCooldown) : 0f;
        }

        /// <summary>Идёт ли замах прямо сейчас (включая кадр удара).</summary>
        public bool IsAttacking { get; private set; }

        /// <summary>Цель текущего замаха; <c>null</c>, если атаки нет.</summary>
        public Transform Target => _target;

        /// <summary>Дистанция атаки из конфига, м.</summary>
        public float Range => _range;

        /// <summary>Полный угол сектора атаки из конфига, градусы.</summary>
        public float Angle => _halfAngle * 2f;

        /// <summary>Остаток паузы после замаха, с; 0 — атаковать можно.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownRemaining);

        public bool IsOnCooldown => _cooldownRemaining > 0f;

        /// <summary>Попадание зафиксировано и ещё никем не забрано (<see cref="TryConsumeHit"/>).</summary>
        public bool HasPendingHit => _hitLanded;

        /// <summary>
        /// Цель в дистанции атаки, в секторе и не позади существа. Смерть/неуязвимость цели
        /// проверяет потребитель (DINO-028); мёртвую цель отсекает зрение, здесь — только геометрия.
        /// </summary>
        public bool IsTargetInAttackArea(Transform target)
        {
            if (_self == null || target == null)
            {
                return false;
            }

            Vector3 offset = target.position - _self.position;
            if (Mathf.Abs(offset.y) > VerticalTolerance)
            {
                return false;
            }

            offset.y = 0f;
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance > _range * _range)
            {
                return false;
            }

            if (sqrDistance <= Mathf.Epsilon)
            {
                return true;
            }

            Vector3 forward = _self.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                return true;
            }

            float angle = Vector3.Angle(forward, offset);
            return angle <= _halfAngle;
        }

        /// <summary>
        /// Можно ли начать замах по цели: не идёт замах, пауза прошла, цель в секторе.
        /// Видимость цели проверяет вызывающее состояние.
        /// </summary>
        public bool CanStartAttack(Transform target)
        {
            if (IsAttacking || IsOnCooldown)
            {
                return false;
            }

            return IsTargetInAttackArea(target);
        }

        /// <summary>Начинает замах, если <see cref="CanStartAttack"/> разрешает.</summary>
        public bool TryStartAttack(Transform target)
        {
            if (!CanStartAttack(target))
            {
                return false;
            }

            IsAttacking = true;
            _target = target;
            _hitTarget = null;
            _windupElapsed = 0f;
            _hitChecked = false;
            _hitLanded = false;
            return true;
        }

        /// <summary>
        /// Единственная проверка попадания за замах: фиксирует результат и позицию цели.
        /// Вызывается либо таймером <c>AttackHitTime</c> (если Animation Event не настроен),
        /// либо Animation Event анимации атаки (<see cref="NotifyAnimationHit"/>) — что наступит первым.
        /// </summary>
        private void CheckHit()
        {
            _hitChecked = true;
            _hitLanded = _target != null && IsTargetInAttackArea(_target);
            _hitTarget = _hitLanded ? _target : null;

            Debug.Log(
                _hitLanded
                    ? $"CreatureAttack: удар попал по «{_hitTarget.name}» (попадание передаётся потребителю)."
                    : "CreatureAttack: удар промахнулся — цель вне дистанции/сектора.",
                _self);
        }

        /// <summary>
        /// Точка входа Animation Event клипа атаки (DINO-029): проверка попадания выполняется
        /// ровно в кадре события. Если событие не настроено или пришло вне замаха, таймер
        /// <c>AttackHitTime</c> остаётся запасным путём; повторной проверки в замахе не будет.
        /// </summary>
        public void NotifyAnimationHit()
        {
            if (!IsAttacking || _hitChecked)
            {
                return;
            }

            CheckHit();
        }

        /// <summary>
        /// Продвигает замах и паузу. Проверка попадания выполняется ровно один раз за замах —
        /// в момент <c>AttackHitTime</c>; повторно в этом замахе она не запускается.
        /// </summary>
        public void Tick(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
            }

            if (!IsAttacking)
            {
                return;
            }

            _windupElapsed += dt;

            // Запасной путь, если Animation Event не настроен: попадание проверяется по таймеру.
            // Если событие уже сработало, повторной проверки нет.
            if (!_hitChecked && _windupElapsed >= _hitTime)
            {
                CheckHit();
            }

            if (_windupElapsed < _windup)
            {
                return;
            }

            IsAttacking = false;
            _target = null;
            _hitTarget = null;

            // Пауза отсчитывается от конца замаха, поэтому замах всегда доигрывается целиком.
            _cooldownRemaining = _cooldown;
        }

        /// <summary>
        /// Забирает результат единственной проверки попадания. Возвращает <c>true</c> один раз
        /// за замах; <paramref name="target"/> — цель удара.
        /// </summary>
        public bool TryConsumeHit(out Transform target)
        {
            target = null;
            if (!_hitLanded)
            {
                return false;
            }

            _hitLanded = false;
            target = _hitTarget;
            _hitTarget = null;
            return target != null;
        }

        /// <summary>Прерывает замах без удара и без запуска паузы (например, при смене поведения).</summary>
        public void Cancel()
        {
            if (!IsAttacking)
            {
                return;
            }

            IsAttacking = false;
            _target = null;
            _hitTarget = null;
            _hitChecked = true;
            _hitLanded = false;
        }
    }
}
