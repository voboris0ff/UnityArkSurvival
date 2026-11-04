using ArkSurvival.AI.Creatures.CreatureStates;
using ArkSurvival.AI.Perception;
using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Набор состояний поведения существа: Idle, Patrol, Investigate, Search, Chase и Return.
    /// Переходы по восприятию собраны в одном месте (<see cref="ApplyPerception"/>), а не размазаны по состояниям.
    /// Idle без цели ведёт в <see cref="Patrol"/>: существо ходит вокруг текущей домашней точки.
    /// Осмотр (Investigate) завершается прибытием или таймером и сообщает об этом через
    /// <see cref="NotifyInvestigationFinished"/>: та же точка больше не осматривается, но существо
    /// переходит в <see cref="Search"/> — ограниченный по времени поиск вокруг последней известной позиции.
    /// </summary>
    public sealed class CreatureStateMachine
    {
        /// <summary>
        /// Насколько точка нового шума должна отличаться от области текущего поиска, чтобы осмотр
        /// переключился на неё: шум рядом с уже обыскиваемым местом не сбивает поиск (временное, DINO-023).
        /// </summary>
        private const float NoiseRetargetDistance = 3f;

        private CreatureState _current;
        private bool _wasPursuing;
        private bool _investigationSpent;

        public CreatureContext Context { get; }
        public IdleState Idle { get; }
        public PatrolState Patrol { get; }
        public InvestigateState Investigate { get; }
        public SearchState Search { get; }
        public ChaseState Chase { get; }
        public ReturnState Return { get; }

        public CreatureState Current => _current;
        public string CurrentName => _current != null ? _current.GetType().Name : string.Empty;

        public CreatureStateMachine(CreatureContext context)
        {
            Context = context;
            Idle = new IdleState(this);
            Patrol = new PatrolState(this);
            Investigate = new InvestigateState(this);
            Search = new SearchState(this);
            Chase = new ChaseState(this);
            Return = new ReturnState(this, context.Navigator.Agent);
        }

        public void Start()
        {
            ChangeState(Idle);
        }

        public void Tick(float deltaTime)
        {
            _current?.Tick(deltaTime);
        }

        /// <summary>
        /// Единая точка перехода по зрению и памяти. Вызывается тиком восприятия:
        /// вижу цель → Chase; цель пропала, но память жива → Investigate;
        /// память истекла (или цель уничтожена) → выход из преследования/осмотра в Idle.
        /// </summary>
        public void ApplyPerception()
        {
            bool seesTarget = Context.Perception.TargetVisible;
            bool remembersTarget = Context.Memory.HasTarget;

            if (seesTarget)
            {
                // Новое наблюдение сбрасывает отметку «осмотр уже выполнен»: следующая потеря цели
                // снова даёт право на осмотр новой точки.
                _investigationSpent = false;

                bool wasPursuing = _wasPursuing;
                _wasPursuing = true;
                if (!wasPursuing)
                {
                    Debug.Log(
                        $"CreatureStateMachine: цель в поле зрения («{Context.Perception.Target?.name}») — Chase.",
                        Context.Navigator.Agent);
                }

                ChangeState(Chase);
                return;
            }

            if (remembersTarget && !_investigationSpent)
            {
                bool wasPursuing = _wasPursuing;
                _wasPursuing = true;
                if (!wasPursuing)
                {
                    Debug.Log(
                        "CreatureStateMachine: цель пропала из виду, но есть в памяти — Investigate.",
                        Context.Navigator.Agent);
                }

                ChangeState(Investigate);
                return;
            }

            if (remembersTarget)
            {
                // Осмотр этой точки уже закончен (прибытие/таймер), память ещё жива:
                // повторно не идём, иначе прибытие каждый кадр запускало бы Investigate заново.
                // Поиск вокруг точки (Search) при этом продолжается — его не прерываем.
                if (_current == Investigate || _current == Chase)
                {
                    _wasPursuing = false;
                    ChangeState(Idle);
                }

                return;
            }

            if (!_wasPursuing)
            {
                return;
            }

            _wasPursuing = false;
            _investigationSpent = true;
            Debug.Log(
                "CreatureStateMachine: память о цели истекла — выход из преследования/осмотра.",
                Context.Navigator.Agent);

            // Возврат домой по-прежнему только по команде (клавиша R); после неудачного поиска
            // домашняя точка уже перенесена на место окончания погони (SearchState), дальше — Idle,
            // который без цели сразу ведёт в Patrol.
            // Незаконченный осмотр переходит в Search: последняя известная точка ещё годится
            // как центр поиска, а Investigate не должен «съедать» поиск из-за короткой памяти.
            if (_current == Investigate)
            {
                ChangeState(Search);
                return;
            }

            if (_current == Chase)
            {
                ChangeState(Idle);
            }
        }

        /// <summary>
        /// Подключает услышанный шум к Investigate с учётом приоритетов (DINO-023):
        /// зрение важнее слуха (видимая цель не прерывается шумом), активная погоня и возврат
        /// не перебиваются; во время поиска шум внутри обыскиваемой области поиск не меняет,
        /// а точка снаружи переключает на её осмотр; в патруле/ожидании шум запускает осмотр.
        /// Шум даёт только приблизительную позицию: она запоминается как последняя известная,
        /// но цель из памяти не назначается, поэтому игрок не становится постоянно видимой целью.
        /// </summary>
        public void ApplyNoise()
        {
            if (Context.Hearing == null || !Context.Hearing.TryConsumeHeardNoise(out NoiseEvent noise))
            {
                return;
            }

            // Зрение всегда важнее слуха: видимую цель шум не перебивает.
            if (Context.Perception.TargetVisible)
            {
                Debug.Log(
                    $"CreatureStateMachine: услышан шум в {noise.Position}, но цель видна — шум не меняет поведение.",
                    Context.Navigator.Agent);
                return;
            }

            // Активная погоня (живая память о цели) шумом не прерывается.
            if (_current == Chase)
            {
                Debug.Log(
                    $"CreatureStateMachine: услышан шум в {noise.Position}, но {CurrentName} не прерывается.",
                    Context.Navigator.Agent);
                return;
            }

            // Возврат домой — только по команде, шум его не отменяет.
            if (_current == Return)
            {
                Debug.Log(
                    $"CreatureStateMachine: услышан шум в {noise.Position}, но {CurrentName} не прерывается.",
                    Context.Navigator.Agent);
                return;
            }

            if (Context.Navigator == null || !Context.Navigator.IsReady)
            {
                return;
            }

            // Поиск уже идёт вокруг этой области — шум рядом не сбивает его на новый осмотр.
            if (_current == Search && IsInsideSearchArea(noise.Position))
            {
                Debug.Log(
                    $"CreatureStateMachine: услышан шум в {noise.Position} — внутри области поиска, поиск продолжается.",
                    Context.Navigator.Agent);
                return;
            }

            // Осмотр уже идёт почти в этой же точке — не перезапускаем его.
            if (_current == Investigate && IsNearLastKnown(noise.Position, NoiseRetargetDistance))
            {
                Context.Memory.RememberPoint(noise.Position);
                Debug.Log(
                    $"CreatureStateMachine: услышан шум в {noise.Position} — рядом с осматриваемой точкой, осмотр продолжается.",
                    Context.Navigator.Agent);
                return;
            }

            bool wasInvestigating = _current == Investigate;

            Context.Memory.RememberPoint(noise.Position);
            _wasPursuing = true;
            _investigationSpent = false;

            Debug.Log(
                $"CreatureStateMachine: шум от «{noise.SourceName}» в {noise.Position} — Investigate к приблизительной позиции.",
                Context.Navigator.Agent);

            ChangeState(Investigate);

            // Новая точка шума во время осмотра: ChangeState на то же состояние не срабатывает,
            // поэтому осмотр перезапускается вручную — с новым маршрутом и таймером.
            if (wasInvestigating && _current == Investigate)
            {
                Investigate.Exit();
                Investigate.Enter();
            }
        }

        /// <summary>Точка входит в текущую область поиска (радиус <c>SearchRadius</c> вокруг опоры поиска).</summary>
        private bool IsInsideSearchArea(Vector3 position)
        {
            float radius = Context.Config != null ? Mathf.Max(0f, Context.Config.SearchRadius) : 8f;
            Vector3 offset = position - Search.Anchor;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        /// <summary>Точка ближе порога к последней известной позиции — считается той же точкой.</summary>
        private bool IsNearLastKnown(Vector3 position, float threshold)
        {
            Vector3 offset = position - Context.Memory.LastKnownPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= threshold * threshold;
        }

        /// <summary>
        /// Команда осмотра последней известной позиции (например, когда погоня физически невозможна).
        /// Осмотр имеет смысл только при живой памяти; повторный вызов во время осмотра ничего не меняет.
        /// </summary>
        public void RequestInvestigate()
        {
            if (_current == Investigate || _current == Return)
            {
                return;
            }

            if (Context.Memory.HasTarget)
            {
                _wasPursuing = true;
                _investigationSpent = false;
                ChangeState(Investigate);
                return;
            }

            _wasPursuing = false;
            ChangeState(Idle);
        }

        /// <summary>
        /// Осмотр завершён (прибытие или таймер). Пока память о той же точке жива, повторно
        /// осматривать её не нужно: новая потеря цели сбросит отметку. Дальше — поиск (Search).
        /// </summary>
        public void NotifyInvestigationFinished()
        {
            _investigationSpent = true;
            _wasPursuing = false;
        }

        /// <summary>
        /// Поиск закончился без обнаружения цели. Смену домашней точки на место окончания погони
        /// и сброс памяти выполняет <see cref="SearchState"/> перед вызовом. Отметка «осмотр отработан»
        /// ставится, чтобы живая память не запускала Investigate заново.
        /// </summary>
        public void NotifySearchFinished()
        {
            _investigationSpent = true;
            _wasPursuing = false;
        }

        /// <summary>Команда вернуться к исходной точке. Повторный вызов во время возврата ничего не меняет.</summary>
        public void RequestReturn()
        {
            if (_current == Return)
            {
                return;
            }

            // После возврата не возобновляем осмотр старой точки без нового наблюдения.
            _investigationSpent = true;
            _wasPursuing = false;
            ChangeState(Return);
        }

        public void ChangeState(CreatureState next)
        {
            if (next == null || next == _current)
            {
                return;
            }

            Debug.Log($"CreatureStateMachine: {CurrentName} → {next.GetType().Name}.", Context.Navigator.Agent);
            _current?.Exit();
            _current = next;
            _current.Enter();
        }
    }
}
