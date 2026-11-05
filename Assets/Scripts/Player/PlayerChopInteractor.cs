using System.Collections.Generic;
using ArkSurvival.Data;
using ArkSurvival.World;
using UnityEngine;

namespace ArkSurvival.Player
{
    /// <summary>
    /// Рубка деревьев игроком (DINO-036, DINO-037): поиск ближайшего дерева в радиусе
    /// <see cref="ChopConfig.ChopRange"/>, секторе <see cref="ChopConfig.ChopAngle"/> вокруг
    /// направления вперёд, только слои <see cref="ChopConfig.TreeLayerMask"/>; срубленные
    /// деревья пропускаются. По нажатию начинается замах: без цели, во время паузы
    /// <see cref="ChopConfig.ChopCooldown"/> и у мёртвого игрока он не запускается. Начатый
    /// замах не отменяется; ровно один удар наносится Animation Event клипа или, если события
    /// нет, таймером <see cref="ChopConfig.ChopHitTime"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerChopInteractor : MonoBehaviour
    {
        private const int OverlapCapacity = 32;

        [Tooltip("Параметры рубки. Пусто — поиск деревьев отключается с предупреждением.")]
        [SerializeField] private ChopConfig _config;

        private readonly Collider[] _overlaps = new Collider[OverlapCapacity];
        private readonly Dictionary<Collider, ChoppableTree> _treeCache =
            new Dictionary<Collider, ChoppableTree>();

        private PlayerController _player;
        private PlayerInputReader _input;
        private PlayerAnimator _animator;
        private ChoppableTree _swingTarget;
        private float _swingElapsed;
        private float _cooldownRemaining;
        private bool _hitApplied;

        /// <summary>Ближайшее дерево в секторе рубки; null, если цели нет.</summary>
        public ChoppableTree CurrentTarget { get; private set; }

        /// <summary>Есть ли дерево, по которому можно ударить.</summary>
        public bool HasTarget => CurrentTarget != null;

        /// <summary>Идёт ли замах прямо сейчас (включая кадр удара).</summary>
        public bool IsSwinging { get; private set; }

        /// <summary>Остаток паузы после замаха, с; 0 — рубить можно.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownRemaining);

        public bool IsOnCooldown => _cooldownRemaining > 0f;

        /// <summary>Дерево текущего замаха; <c>null</c>, если замаха нет.</summary>
        public ChoppableTree SwingTarget => _swingTarget;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _input = GetComponent<PlayerInputReader>();
            _animator = GetComponent<PlayerAnimator>();

            if (_config == null)
            {
                Debug.LogWarning(
                    "PlayerChopInteractor: ChopConfig не назначен — поиск деревьев отключён. " +
                    "Назначьте ассет ChopConfig.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_config == null)
            {
                // Конфиг мог быть снят в инспекторе после Awake: начатый замах безопасно сбрасываем.
                IsSwinging = false;
                _swingTarget = null;
                CurrentTarget = null;
                return;
            }

            float deltaTime = Time.deltaTime;

            // Начатый замах всегда доигрывается: смерть игрока его не отменяет.
            TickSwing(deltaTime);

            if (_player != null && _player.IsDead)
            {
                CurrentTarget = null;
                return;
            }

            CurrentTarget = FindTarget();

            if (_input == null || !_input.ChopPressed)
            {
                return;
            }

            TryStartSwing();
        }

        private void TryStartSwing()
        {
            if (IsSwinging || IsOnCooldown || CurrentTarget == null)
            {
                return;
            }

            IsSwinging = true;
            _swingTarget = CurrentTarget;
            _swingElapsed = 0f;
            _hitApplied = false;

            if (_animator != null)
            {
                _animator.TriggerChop();
            }
        }

        private void TickSwing(float deltaTime)
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
            }

            if (!IsSwinging)
            {
                return;
            }

            _swingElapsed += deltaTime;

            // Ровно один удар за замах: запасной путь, если Animation Event клипа не сработал (DINO-038).
            if (!_hitApplied && _swingElapsed >= _config.ChopHitTime)
            {
                ApplyChopHit();
            }

            if (_swingElapsed < _config.ChopSwingDuration)
            {
                return;
            }

            IsSwinging = false;
            _swingTarget = null;

            // Пауза отсчитывается от конца замаха, поэтому замах доигрывается целиком.
            _cooldownRemaining = _config.ChopCooldown;
        }

        /// <summary>
        /// Заглушка урона: одно попадание по цели замаха. Мёртвое (срубленное или уничтоженное)
        /// дерево молча пропускается.
        /// </summary>
        /// <summary>
        /// Точка входа Animation Event клипа замаха (DINO-038): удар наносится ровно в кадре
        /// события. Если событие не настроено или пришло вне замаха, удар применяет таймер
        /// <see cref="ChopConfig.ChopHitTime"/>; повторного удара в замахе не будет.
        /// </summary>
        public void NotifyAnimationHit()
        {
            if (!IsSwinging || _hitApplied)
            {
                return;
            }

            ApplyChopHit();
        }

        private void ApplyChopHit()
        {
            _hitApplied = true;

            if (_swingTarget == null || _swingTarget.IsFelled)
            {
                return;
            }

            _swingTarget.TakeChopDamage(_config.ChopDamage, transform.position);
        }

        private ChoppableTree FindTarget()
        {
            Vector3 origin = transform.position;
            float range = _config.ChopRange;
            float halfAngle = _config.ChopAngle * 0.5f;
            float rangeSqr = range * range;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            int count = Physics.OverlapSphereNonAlloc(
                origin,
                range,
                _overlaps,
                _config.TreeLayerMask,
                QueryTriggerInteraction.Ignore);

            ChoppableTree nearest = null;
            float nearestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                ChoppableTree tree = ResolveTree(collider);
                if (tree == null || tree.IsFelled)
                {
                    continue;
                }

                // Ближайшая точка коллайдера: расстояние считается по горизонтали,
                // чтобы высокий ствол не отбрасывался из-за разницы высот.
                Vector3 delta = collider.ClosestPoint(origin) - origin;
                delta.y = 0f;
                float sqrDistance = delta.sqrMagnitude;
                if (sqrDistance > rangeSqr)
                {
                    continue;
                }

                if (sqrDistance > 0.0001f && Vector3.Angle(forward, delta) > halfAngle)
                {
                    continue;
                }

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = tree;
                }
            }

            return nearest;
        }

        private ChoppableTree ResolveTree(Collider collider)
        {
            if (_treeCache.TryGetValue(collider, out ChoppableTree cached))
            {
                // Уничтоженный объект в Unity сравнивается с null.
                if (cached != null)
                {
                    return cached;
                }

                _treeCache.Remove(collider);
            }

            ChoppableTree tree = collider.GetComponentInParent<ChoppableTree>();
            if (tree != null)
            {
                _treeCache[collider] = tree;
            }

            return tree;
        }
    }
}
