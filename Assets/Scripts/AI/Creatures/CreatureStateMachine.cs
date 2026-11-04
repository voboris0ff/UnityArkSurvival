using ArkSurvival.AI.Creatures.CreatureStates;
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
