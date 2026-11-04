using ArkSurvival.AI.Perception;
using ArkSurvival.Combat;
using ArkSurvival.Data;
using UnityEngine;
using UnityEngine.AI;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Точка композиции существа: связывает компоненты, создаёт навигационный адаптер,
    /// валидирует ссылки и тикает машину состояний. Поведением не управляет — решения в состояниях.
    /// </summary>
    public sealed class CreatureController : MonoBehaviour
    {
        private const float TargetSearchRadius = 50f;
        private const float MemoryLogInterval = 1f;

        /// <summary>Радиус проверки, что домашняя точка достижима по NavMesh, м.</summary>
        private const float HomeReachSampleRadius = 2f;

        [SerializeField] private CreatureConfig _config;
        [SerializeField] private NavMeshAgent _agent;

        [Tooltip("Начальная домашняя точка. Пусто — стартовая позиция существа. Дальше точка меняется командой SetHome.")]
        [SerializeField] private Transform _homePoint;

        [Tooltip("Точка глаз для проверки зрения. Пусто — берётся позиция существа + CreatureConfig.EyeHeight.")]
        [SerializeField] private Transform _eyePoint;

        [Tooltip("Цель зрения первой версии — игрок. Пусто — контроллер ищет её сам (объект на слое Player) при старте.")]
        [SerializeField] private Transform _target;

        private CreatureNavigator _navigator;
        private CreatureTargetMemory _memory;
        private CreatureVision _vision;
        private CreaturePerception _perception;
        private CreatureHearing _hearing;
        private CreatureHome _home;
        private CreatureAttack _attack;
        private CreatureStateMachine _stateMachine;
        private Transform _eyeTransform;
        private bool _wasTargetVisible;
        private bool _wasTargetRemembered;
        private float _memoryLogTimer;

        public CreatureConfig Config => _config;
        public CreatureNavigator Navigator => _navigator;
        public CreatureTargetMemory Memory => _memory;
        public CreatureVision Vision => _vision;
        public CreaturePerception Perception => _perception;
        public CreatureHearing Hearing => _hearing;
        public CreatureHome Home => _home;
        public CreatureAttack Attack => _attack;
        public Transform Target => _target;
        public CreatureStateMachine StateMachine => _stateMachine;
        public Transform HomePoint => _homePoint;
        public Vector3 HomePosition => _home != null ? _home.Position : _homePoint != null ? _homePoint.position : transform.position;
        public bool IsInitialized => _navigator != null && _navigator.IsValid;

        private void Awake()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }
        }

        private void Start()
        {
            if (!ValidateReferences())
            {
                return;
            }

            if (_target == null)
            {
                _target = FindTargetOnPlayerLayer();
            }

            if (_target == null)
            {
                Debug.LogWarning(
                    "CreatureController: цель зрения не назначена и на слое Player не найдена — зрение никого не увидит.",
                    this);
            }
            else
            {
                WarnIfTargetNotOnPlayerLayer(_target);
            }

            _navigator = new CreatureNavigator(_agent);
            _navigator.Initialize(_config.ArrivalDistance, _config.MoveSpeed, _config.TurnSpeed);
            _memory = new CreatureTargetMemory(_config.MemoryDuration);

            // Домашняя точка инициализируется один раз от стартовой позиции (или назначенного Home Point)
            // и дальше меняется только командой SetHome — она не следует за существом.
            _home = new CreatureHome(_agent);
            _home.Initialize(_homePoint != null ? _homePoint.position : transform.position);

            // Домашняя точка вне NavMesh: патруль вокруг неё не найдёт ни одной точки,
            // а Return не построит путь. Предупреждаем один раз при старте (DINO-031).
            if (_navigator.IsReady && !_navigator.CanReach(_home.Position, HomeReachSampleRadius))
            {
                Debug.LogWarning(
                    $"CreatureController: домашняя точка {_home.Position} недостижима по NavMesh — " +
                    "патруль и возврат работать не будут. Проверьте NavMesh вокруг дома.",
                    this);
            }

            _eyeTransform = ResolveEyePoint();
            _vision = new CreatureVision(_eyeTransform, _config);
            _perception = new CreaturePerception(_target, _vision, _config.PerceptionInterval);
            _hearing = new CreatureHearing(transform, _config);
            _attack = new CreatureAttack(transform, _config);

            if (_vision.ObstacleLayerMask == 0)
            {
                Debug.LogWarning(
                    "CreatureController: ObstacleLayerMask зрения пуст — препятствия не будут перекрывать взгляд.",
                    this);
            }

            if (!_navigator.IsReady)
            {
                Debug.LogError(
                    "CreatureController: NavMeshAgent не активен или существо не стоит на NavMesh. " +
                    "Проверьте, что NavMesh запечён и объект находится на поверхности.",
                    this);
            }

            _stateMachine = new CreatureStateMachine(
                new CreatureContext(_navigator, _memory, _vision, _perception, _hearing, _config, _home, _attack));

            _stateMachine.Start();

            if (_target != null)
            {
                Debug.Log(
                    $"CreatureController: существо готово. Цель «{_target.name}», зрение {_config.SightDistance} м / " +
                    $"{_config.SightAngle}°, слух {_config.HearingRange} м, тик восприятия {_config.PerceptionInterval} с, " +
                    $"память {_config.MemoryDuration} с.",
                    this);
            }
        }

        /// <summary>
        /// Подписка на глобальное событие шума живёт ровно пока объект включён: Start вызывается
        /// один раз, а объект могут выключить и включить. Обработчик сам ждёт готовности слуха,
        /// поэтому подписка до Start безопасна.
        /// </summary>
        private void OnEnable()
        {
            NoiseBroadcaster.Emitted += OnNoiseEmitted;
        }

        private void OnDisable()
        {
            NoiseBroadcaster.Emitted -= OnNoiseEmitted;
        }

        /// <summary>
        /// Приём шума: слышимость считает <see cref="CreatureHearing"/>. Здесь только подписка
        /// на глобальное событие и лог; переход в Investigate выполняет
        /// <see cref="CreatureStateMachine.ApplyNoise"/> в Update (DINO-021).
        /// </summary>
        private void OnNoiseEmitted(NoiseEvent noise)
        {
            if (_hearing == null || !_hearing.Hear(noise))
            {
                return;
            }

            Debug.Log(
                $"CreatureController: услышал шум от «{noise.SourceName}» в {noise.Position}, " +
                $"громкость {noise.Loudness:F2}, дистанция {_hearing.LastDistance:F1} м из " +
                $"{_hearing.EffectiveRange(noise.Loudness):F1} м.",
                this);
        }

        /// <summary>Команда вернуться к исходной точке. Реакция на события (страх, хп и т.п.) — отдельная задача.</summary>
        public void RequestReturn()
        {
            if (_stateMachine == null)
            {
                Debug.LogWarning(
                    "CreatureController: RequestReturn вызван до инициализации (Start не завершён) — команда проигнорирована.",
                    this);
                return;
            }

            if (!_navigator.IsReady)
            {
                Debug.LogWarning(
                    "CreatureController: RequestReturn — NavMeshAgent не готов (нет NavMesh или агент выключен).",
                    this);
                return;
            }

            Debug.Log($"CreatureController: запрос возврата, текущее состояние {_stateMachine.CurrentName}.", this);
            _stateMachine.RequestReturn();
            Debug.Log($"CreatureController: новое состояние {_stateMachine.CurrentName}.", this);
        }

        /// <summary>
        /// Меняет домашнюю точку существа с привязкой к NavMesh. По ходу погони точку переносит
        /// <see cref="CreatureStates.SearchState"/> при неудачном завершении поиска (DINO-016);
        /// активный патруль замечает смену точки и выбирает новые маршруты вокруг неё (DINO-018).
        /// </summary>
        public bool SetHome(Vector3 position)
        {
            if (_home == null)
            {
                Debug.LogWarning(
                    "CreatureController: SetHome вызван до инициализации (Start не завершён) — команда проигнорирована.",
                    this);
                return false;
            }

            return _home.SetHome(position);
        }

        private void Update()
        {
            if (_stateMachine == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            // Тик восприятия идёт всегда, независимо от состояния: дорогие проверки внутри
            // выполняются только раз в PerceptionInterval. Память тикает каждый кадр,
            // поэтому давность наблюдения считается верно даже при интервале 0.
            _perception.Tick(deltaTime);
            _memory.Tick(deltaTime);

            LogPerceptionChanges();
            _stateMachine.ApplyPerception();

            // Шум обрабатывается после зрения: видимая цель имеет приоритет, а услышанный шум
            // даёт лишь приблизительную точку для Investigate (DINO-021).
            _stateMachine.ApplyNoise();

            _stateMachine.Tick(deltaTime);

            // Замах и пауза атаки тикают каждый кадр независимо от состояния: начатый замах
            // не отменяется сменой поведения (DINO-027). Попадание фиксируется один раз (DINO-026)
            // и сразу применяется к цели (DINO-028).
            _attack.Tick(deltaTime);
            ConsumeAttackHit();

            // Поворот — единственная точка управления вращением: NavMeshAgent только прокладывает путь,
            // а угловую скорость ограничивает CreatureConfig.TurnSpeed.
            _navigator.TickRotation(deltaTime);
        }

        /// <summary>
        /// Применяет единственное за замах попадание (DINO-028): тот же кадр, в котором
        /// <see cref="CreatureAttack"/> зафиксировал удар. Попадание забирается ровно один раз,
        /// поэтому повторной попытки в этом замахе нет ни при промахе, ни при <c>false</c>
        /// от <see cref="IKillable.TryKill"/> (цель уже мертва или в i-frames).
        /// </summary>
        private void ConsumeAttackHit()
        {
            if (!_attack.TryConsumeHit(out Transform hitTarget) || hitTarget == null)
            {
                return;
            }

            IKillable killable = hitTarget.GetComponentInParent<IKillable>();
            if (killable == null)
            {
                Debug.Log(
                    $"CreatureController: попадание по «{hitTarget.name}», но у цели нет IKillable — смерть не применяется.",
                    this);
                return;
            }

            bool killed = killable.TryKill(transform.position);
            if (!killed)
            {
                Debug.Log(
                    $"CreatureController: попадание по «{hitTarget.name}» не принято (мертва или неуязвима) — повтор в этом замахе не выполняется.",
                    this);
                return;
            }

            Debug.Log(
                $"CreatureController: цель «{hitTarget.name}» убита — память сброшена, поведение Patrol.",
                this);

            // Замах/пауза снимаются, память очищается, состояние — Patrol: мёртвую цель не преследуют.
            _stateMachine.NotifyTargetKilled();
        }

        /// <summary>
        /// Пишет в Console каждое изменение обзора и памяти (не каждый кадр) — чтобы ручная проверка
        /// в Play Mode была понятной без чтения инспектора.
        /// </summary>
        private void LogPerceptionChanges()
        {
            bool visible = _perception.HasResult && _perception.TargetVisible;
            if (visible != _wasTargetVisible)
            {
                _wasTargetVisible = visible;
                if (visible)
                {
                    Debug.Log(
                        $"CreatureController: вижу цель «{GetTargetName()}» в точке {_perception.VisiblePoint}, " +
                        $"дистанция {Vector3.Distance(_eyeTransform.position, _perception.VisiblePoint):F1} м.",
                        this);
                }
                else
                {
                    Debug.Log("CreatureController: цель пропала из поля зрения.", this);
                }
            }

            bool remembered = _memory.HasTarget;
            if (remembered != _wasTargetRemembered)
            {
                _wasTargetRemembered = remembered;
                _memoryLogTimer = 0f;
                if (remembered)
                {
                    Debug.Log(
                        $"CreatureController: цель запомнена, последняя позиция {_memory.LastKnownPosition}, " +
                        $"память {_memory.MemoryDuration} с.",
                        this);
                }
                else
                {
                    Debug.Log(
                        $"CreatureController: память о цели истекла (не вижу {_memory.TimeSinceLastSeen:F1} с).",
                        this);
                }
            }

            // Раз в секунду, пока цель не видна, но ещё в памяти: видно, что давность растёт и позиция обновляется.
            if (!remembered || visible)
            {
                _memoryLogTimer = 0f;
                return;
            }

            _memoryLogTimer += Time.deltaTime;
            if (_memoryLogTimer >= MemoryLogInterval)
            {
                _memoryLogTimer = 0f;
                Debug.Log(
                    $"CreatureController: память о цели жива, не вижу {_memory.TimeSinceLastSeen:F1} с, " +
                    $"позиция {_memory.LastKnownPosition}, состояние {_stateMachine.CurrentName}.",
                    this);
            }
        }

        private string GetTargetName()
        {
            // Уничтоженная цель: ?. не ловит «фальшивый null» Unity и упал бы на .name.
            return _target != null ? _target.name : "destroyed";
        }

        private bool ValidateReferences()
        {
            if (_config == null)
            {
                Debug.LogError("CreatureController: CreatureConfig не назначен — существо не запущено.", this);
                return false;
            }

            if (_agent == null)
            {
                Debug.LogError("CreatureController: NavMeshAgent не найден — существо не запущено.", this);
                return false;
            }

            return true;
        }

        /// <summary>Точка глаз: назначенный Transform, иначе временный дочерний объект на высоте CreatureConfig.EyeHeight.</summary>
        private Transform ResolveEyePoint()
        {
            if (_eyePoint != null)
            {
                return _eyePoint;
            }

            GameObject eye = new GameObject("EyePoint");
            eye.transform.SetParent(transform, false);
            eye.transform.localPosition = new Vector3(0f, Mathf.Max(0f, _config.EyeHeight), 0f);
            return eye.transform;
        }

        /// <summary>
        /// Объект не на слое Player виден зрению хуже: если слой цели входит в ObstacleLayerMask,
        /// собственный коллайдер цели перекрывает луч и цель считается невидимой.
        /// </summary>
        private void WarnIfTargetNotOnPlayerLayer(Transform target)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0 || target.gameObject.layer == playerLayer)
            {
                return;
            }

            Debug.LogWarning(
                $"CreatureController: объект «{target.name}» в Target не на слое Player — " +
                "назначьте ему слой Player, иначе зрение может считать его препятствием.",
                this);
        }

        /// <summary>
        /// Цель первой версии зрения — игрок. Ищется один раз при старте (не в Update):
        /// объект на слое Player, у которого есть хотя бы Collider.
        /// Радиус поиска берётся с запасом — цель может позже подойти к границе обзора.
        /// </summary>
        private Transform FindTargetOnPlayerLayer()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0)
            {
                Debug.LogWarning("CreatureController: слой Player не найден в TagManager — автопоиск цели недоступен.", this);
                return null;
            }

            float radius = Mathf.Max(TargetSearchRadius, _config.SightDistance);
            Collider[] colliders = Physics.OverlapSphere(
                transform.position,
                radius,
                1 << playerLayer,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                return collider.transform;
            }

            return null;
        }
    }
}
