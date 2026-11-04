namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Покой: существо стоит на месте. Переходы в Investigate/Chase по зрению и памяти
    /// выполняет <see cref="CreatureStateMachine.ApplyPerception"/>;
    /// Return запускается командой <see cref="CreatureStateMachine.RequestReturn"/>.
    /// Основное поведение без цели — патруль: пока агент готов, Idle ведёт в
    /// <see cref="PatrolState"/> (DINO-018).
    /// </summary>
    public sealed class IdleState : CreatureState
    {
        public IdleState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            if (Machine.Context.Navigator.IsValid)
            {
                Machine.Context.Navigator.Stop();
            }
        }

        public override void Tick(float deltaTime)
        {
            // Без готового агента патрулировать некуда — остаёмся в покое.
            if (Machine.Current == this && Machine.Context.Navigator.IsReady)
            {
                Machine.ChangeState(Machine.Patrol);
            }
        }
    }
}
