using ArkSurvival.Data;
using UnityEngine;

namespace ArkSurvival.AI.Creatures.CreatureStates
{
    /// <summary>
    /// Ближняя атака (DINO-027): состояние запускает замах через <see cref="CreatureAttack"/>,
    /// пока замах идёт — стоит на месте и ограниченно доворачивается к цели
    /// (<c>CreatureConfig.TurnSpeed</c>), после замаха возвращается в <see cref="ChaseState"/>.
    /// Начатый замах не отменяется сменой поведения: переходы восприятия и шума
    /// в <see cref="CreatureStateMachine"/> его не прерывают.
    /// </summary>
    public sealed class AttackState : CreatureState
    {
        /// <summary>Ограничение доворота, если <c>TurnSpeed</c> не задан (без ограничения), градусы/с.</summary>
        private const float UnlimitedTurnSpeed = 720f;

        public AttackState(CreatureStateMachine machine) : base(machine)
        {
        }

        public override void Enter()
        {
            CreatureContext context = Machine.Context;

            if (context.Attack == null)
            {
                RedirectToChase();
                return;
            }

            Transform target = context.Perception.Target;
            bool canAttack = context.Perception.TargetVisible &&
                             target != null &&
                             context.Attack.TryStartAttack(target);

            if (!canAttack)
            {
                // Условия старта уже не выполняются (цель ушла из сектора, пауза не прошла) —
                // атака не начинается, поведение продолжает погоню.
                RedirectToChase();
                return;
            }

            // Во время замаха существо стоит: маршрут снимается, иначе агент проедет сквозь цель.
            if (context.Navigator != null)
            {
                context.Navigator.Stop();
            }

            Debug.Log(
                $"AttackState: замах по цели «{target.name}», дистанция {Vector3.Distance(context.Navigator.Position, target.position):F1} м.",
                context.Navigator.Agent);
        }

        public override void Exit()
        {
            CreatureContext context = Machine.Context;

            // Замах не отменяем: <see cref="CreatureAttack"/> доигрывает его сам.
            // Здесь только состояние — снова можно двигаться.
            if (context.Navigator != null && context.Navigator.IsReady)
            {
                context.Navigator.ResetToSpeed();
            }
        }

        public override void Tick(float deltaTime)
        {
            // Машина могла уже переключить состояние (например, восприятие завершило замах) — не мешаем.
            if (Machine.Current != this)
            {
                return;
            }

            CreatureContext context = Machine.Context;

            if (context.Attack == null || !context.Attack.IsAttacking)
            {
                RedirectToChase();
                return;
            }

            // Замах длится дольше тика восприятия: освежаем память каждый кадр, пока цель видна,
            // иначе короткая MemoryDuration могла бы «забыть» цель прямо во время атаки.
            Transform target = context.Attack.Target;
            if (target != null && context.Perception.TargetVisible && context.Memory != null)
            {
                context.Memory.Observe(target, target.position);
            }

            TurnTowardsTarget(context, target, Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// Доворот к цели с тем же ограничением угловой скорости, что и у движения:
        /// существо не «щёлкает» на цель мгновенно, а успевает только частично довернуться.
        /// </summary>
        private void TurnTowardsTarget(CreatureContext context, Transform target, float deltaTime)
        {
            if (target == null || context.Navigator == null || !context.Navigator.IsReady)
            {
                return;
            }

            Transform self = context.Navigator.Agent.transform;
            Vector3 direction = target.position - self.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            float turnSpeed = GetTurnSpeed(context.Config);
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRotation, turnSpeed * deltaTime);
        }

        private static float GetTurnSpeed(CreatureConfig config)
        {
            if (config == null)
            {
                return UnlimitedTurnSpeed;
            }

            return config.TurnSpeed > 0f ? config.TurnSpeed : UnlimitedTurnSpeed;
        }

        private void RedirectToChase()
        {
            Machine.ChangeState(Machine.Chase);
        }
    }
}
