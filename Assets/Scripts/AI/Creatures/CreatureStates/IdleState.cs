namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Покой: существо стоит на месте. Переход в Investigate/Chase появится вместе с восприятием
    /// и памятью о цели (DINO-006+); Return запускается командой <see cref="CreatureStateMachine.RequestReturn"/>.
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
        }
    }
}
