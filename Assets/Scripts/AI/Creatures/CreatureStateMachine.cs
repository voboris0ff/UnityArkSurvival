using ArkSurvival.AI.Creatures.CreatureStates;
using UnityEngine;

namespace ArkSurvival.AI.Creatures
{
    /// <summary>
    /// Набор состояний поведения существа: Idle, Investigate, Chase и Return.
    /// Переходы по восприятию собраны в одном месте (<see cref="ApplyPerception"/>), а не размазаны по состояниям.
    /// </summary>
    public sealed class CreatureStateMachine
    {
        private CreatureState _current;
        private bool _wasPursuing;

        public CreatureContext Context { get; }
        public IdleState Idle { get; }
        public InvestigateState Investigate { get; }
        public ChaseState Chase { get; }
        public ReturnState Return { get; }

        public CreatureState Current => _current;
        public string CurrentName => _current != null ? _current.GetType().Name : string.Empty;

        public CreatureStateMachine(CreatureContext context)
        {
            Context = context;
            Idle = new IdleState(this);
            Investigate = new InvestigateState(this);
            Chase = new ChaseState(this);
            Return = new ReturnState(this, context.Navigator.Agent, context.HomePosition);
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

            if (remembersTarget)
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

            if (!_wasPursuing)
            {
                return;
            }

            _wasPursuing = false;
            Debug.Log(
                "CreatureStateMachine: память о цели истекла — выход из преследования/осмотра.",
                Context.Navigator.Agent);

            // Возврат домой по-прежнему только по команде (клавиша R): поведение после осмотра — DINO-015.
            if (_current == Investigate || _current == Chase)
            {
                ChangeState(Idle);
            }
        }

        /// <summary>Команда вернуться к исходной точке. Повторный вызов во время возврата ничего не меняет.</summary>
        public void RequestReturn()
        {
            if (_current == Return)
            {
                return;
            }

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
