namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Базовое состояние поведения существа. Состояния принимают решения и вызывают
    /// navigator/memory/attack, но не работают с NavMeshAgent напрямую.
    /// </summary>
    public abstract class CreatureState
    {
        protected readonly CreatureStateMachine Machine;

        protected CreatureState(CreatureStateMachine machine)
        {
            Machine = machine;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public abstract void Tick(float deltaTime);
    }
}
