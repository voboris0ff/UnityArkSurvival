# Roadmap Archive

Completed tasks (status [x]). History is available in `docs/session-logs/`.

## Stage A. First creature: base architecture and navigation

- [x] **DINO-001** `CreatureController` contract.
- [x] **DINO-002** `CreatureConfig` (ScriptableObject class; the `.asset` is created by the user).
- [x] **DINO-003** `CreatureNavigator` over NavMeshAgent (destination, stop, speed, path, safe checks).
- [x] **DINO-004** `CreatureController` — reference composition and validation.
- [x] **DINO-005** `CreatureStateMachine` + `IdleState`.
- [x] **DINO-006** `ReturnState`, `RequestReturn()`; verified in Play Mode (`R`).
- [x] **DINO-007** `CreatureAnimator` publishes `Speed` (float); the `walk`/`run` blend trees are set up by the user.

## Stage B. Vision, target memory and losing the player

- [x] **DINO-008** `CreatureTargetMemory`: target, `LastKnownPosition`, age, `Clear`.
- [x] **DINO-009** `CreatureVision`: distance, view angle, raycast by `ObstacleLayerMask`.
- [x] **DINO-010** `CreaturePerception`: periodic vision tick (`PerceptionInterval`).
- [x] **DINO-011** `ApplyPerception` — the only transition point: sees → `Chase`, remembers → `Investigate`, expired → `Idle`.
- [x] **DINO-012** `ChaseState`: path no more often than `DestinationUpdateInterval` and only when the target shifted more than `MinRepathDistance`.
- [x] **DINO-013** `InvestigateState`: to `LastKnownPosition`, ends on arrival or `InvestigateDuration`; verified by the user.
- [x] **DINO-014** `CreatureHome` — the single home-point store, `SetHome` snapped to the NavMesh.
- [x] **DINO-015** `SearchState`: random points within `SearchRadius` around the last position, pauses, `SearchDuration` limit.
- [x] **DINO-035** `CreatureConfig.TurnSpeed` limits the turn rate (`CreatureNavigator.TickRotation`, `<= 0` — rotation by the agent).

## Stage C. Free patrolling

- [x] **DINO-016** `SearchState.Finish`: `SetHome(Navigator.Position)`, `Memory.Clear()`, switch to `Patrol`.
- [x] **DINO-017** `CreaturePatrolPointSelector` + `Patrol*` parameters in `CreatureConfig`.
- [x] **DINO-018** `PatrolState`: walking between points around home, pauses by `PatrolWaitChance`, point reselection when none is available.

## Stage D. Hearing

- [x] **DINO-019** `NoiseEvent` + `NoiseBroadcaster` (position, loudness 0..1, source).
- [x] **DINO-020** `CreatureHearing`: range `HearingRange * Loudness`, ignores its own noise.
- [x] **DINO-021** `ApplyNoise`: noise → approximate point in memory → `Investigate`; never becomes the target.
- [x] **DINO-022** `PlayerNoiseEmitter`: player sprint steps and landing.
- [x] **DINO-023** Sight and hearing priorities (rules in `project-context.md`): sight > noise, `Chase`/`Return` are not interrupted, `Search` is not disturbed by noise inside its area.

## Stage E. Melee attack and player death

- [x] **DINO-024** Studied player death/respawn: the project has no health or respawn system. The `IKillable.TryKill(Vector3 source)` contract (`false` — invulnerable/dead, `true` — hit accepted) is described in `project-context.md`; the integration point is `PlayerController` (implements the interface, `IsDead` flag, early exit in `Update`). No code changes. Post-death behaviour — `manual-queue.md`.
- [x] **DINO-025** Find the player roll and add i-frames (`IsInvulnerable` by timer, temporary values).
- [x] **DINO-026** `CreatureAttack`: distance, sector, `AttackCooldown` (~1 s from the windup end), windup duration, `HitTime`; the hit is checked once per windup. No damage.
- [x] **DINO-027** `Attack` state: start on conditions + cooldown, `Chase` after the windup; a started windup is not cancelled; the turn is limited.
- [x] **DINO-028** Call `TryKill` at `HitTime`; `false` — a miss without a retry in this windup. After death: memory cleared, no attacks on a dead target, `Patrol`. No second health system and no per-frame damage.
- [x] **DINO-029** Attack animation trigger/event: `Attack` trigger at windup start (`CreatureAnimator`), `OnAttackHit` Animation Event (`CreatureAttackAnimationEvent` → `CreatureAttack.NotifyAnimationHit`); manual clip setup — `manual-queue.md`.

## Stage F. Assembling the creature in Unity and finishing the first version

- [x] **DINO-030** List of mandatory components and references for the creature prefab (the prefab is not edited by code).
- [x] **DINO-031** Static review of edge cases: empty references, invalid agent, no path, destroyed/dead target, unreachable points, home outside the NavMesh. Do not run builds or tests.
- [x] **DINO-032** Fix the bug where the character’s camera sees through the wall if the player gets too close to the wall.

## Stage G. Trees and log chopping

- [x] **DINO-033** `TreeConfig` (ScriptableObject class; the `.asset` is created by the user): `MaxHealth`, `LogsCount`, `LogSpawnImpulse`, `LogSpawnRadius`. No logic.
- [x] **DINO-034** `ChoppableTree`: `Health` from `TreeConfig`, `TakeChopDamage(float amount, Vector3 source)`, `OnDamaged` and `OnFelled` events; the tree is felled once (protection from repeated `OnFelled`, damage after felling is ignored). No spawn and no player interaction.
- [x] **DINO-035** `ChopConfig` (or the `Chop*` parameters in the player config): `ChopDamage`, `ChopRange`, `ChopAngle`, `ChopCooldown`, `TreeLayerMask`. Only the data class and validation of values (`<= 0` → safe defaults).
- [x] **DINO-036** `PlayerChopInteractor`: find the nearest `ChoppableTree` in front of the player (overlap by `TreeLayerMask`, `ChopRange`, `ChopAngle`), skip destroyed/felled trees. No input, no damage.
- [x] **DINO-037** `PlayerChopInteractor`: chop on input, `ChopCooldown`, a swing is not started without a target, not while the player is dead (`IsDead`). A started swing is not cancelled. The damage is a stub (`ApplyChopHit()` is called by timer `HitTime`), exactly one hit per swing.
- [x] **DINO-038** Chop animation: `Chop` trigger at swing start (`PlayerAnimator` or equivalent), `OnChopHit` Animation Event (`PlayerChopAnimationEvent` → `PlayerChopInteractor.NotifyAnimationHit`); the timer from DINO-040 stays as a fallback when there is no event. Manual clip setup — `manual-queue.md`.
- [x] **DINO-039** `LogItem`: component with `Rigidbody` and collider requirements (`RequireComponent`), flags `IsCarried`, `IsRegistered` (for the storage, set later), safe `Destroy`. The log prefab is assembled by the user — list of components and the `LogLayer` in `manual-queue.md`.
- [x] **DINO-040** `TreeLogSpawner`: subscribe to `OnFelled`, spawn `LogsCount` `LogItem`s around the tree (`LogSpawnRadius`, impulse `LogSpawnImpulse`), check the free space, a missing log prefab → a warning and no exception.
- [x] **DINO-041** Remove the felled tree: disable the collider and renderer, destroy the object after the spawn (stump is optional, prefab reference may be empty); the order guarantees that `TreeLogSpawner` finishes first.
- [x] **DINO-042** Tree prefab setup list for the user: `ChoppableTree`, `TreeConfig`, `TreeLogSpawner`, collider, `TreeLayer` (the prefab is not edited by code) — `manual-queue.md`.
- [x] **DINO-043** Исправить баг, описанный пользователем в manual-queue.md
