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

## Stage H. Carrying logs

- [x] **DINO-044** `PlayerLogCarrier`: carried logs counter, `AddLog()`/`RemoveLog()` API returning `bool`, `OnCountChanged` event. No limit, no input, no visuals.
- [x] **DINO-045** `MaxCarriedLogs` in the config, `CanPickUp` (limit check), `AddLog` is refused at the limit, `RemoveLog` never goes below zero; the carrier is never left in an inconsistent state.
- [x] **DINO-046** `PlayerLogPickup`: find the nearest `LogItem` in `PickupRange` (overlap by `LogLayerMask`), skip logs that are carried, registered in the storage reserve or already destroyed. No input and no removal yet.
- [x] **DINO-047** Pick up a log by input: check `CanPickUp`, `AddLog()` first, then remove the world `LogItem`; if `AddLog()` fails — the world object stays untouched.
- [x] **DINO-048** `LogDropPointFinder`: a point in front of the player for a dropped log (raycast down to the ground, `OverlapSphere` free-space check, a few fallback offsets); returns `false` if there is no free place.
- [x] **DINO-049** Drop/put down a log by input: only after `LogDropPointFinder` found a point, spawn `LogItem` (prefab reference), then `RemoveLog()`; no point or no prefab — nothing changes.
- [x] **DINO-050** `CarriedLogsVisual`: N visual logs by `OnCountChanged` (pool or fixed slots up to `MaxCarriedLogs`), offsets for stacking, no colliders and no Rigidbody on the visuals.
- [x] **DINO-051** `CarryAnchor` on the character and the visual log prefab: the user sets up the anchor and the prefab, the list of components and offsets — `manual-queue.md`; a missing anchor → a warning and no visuals, no exception.
- [x] **DINO-052** сейчас полено появляется в земле и иногда проваливается; нужно, чтобы оно появлялось сбоку на уровне рук и получало импульс вперёд
- [x] **DINO-053** Static review of stages G and H: destroyed tree/log during chopping or pickup, empty references, a log picked up twice in one frame, a dead player, drop with no free space. Do not run builds or tests.

## Stage H2. Carrying logs: shoulder pose, sprint ban, dropping logs

- [x] **DINO-054** Studied `PlayerController`: sprint input, roll (start/end, i-frames from DINO-025), jump (takeoff/landing), `PlayerAnimator` parameters, `PlayerNoiseEmitter` triggers. No code changes. The integration points (where the sprint flag is computed, where the roll and jump start) — in `project-context.md`.
- [x] **DINO-055** `PlayerLogCarrier`: read-only `IsCarrying` (`Count > 0`) and `OnCarryStateChanged(bool)` event, raised only when the state changes (`0 ↔ >0`), not on every count change. No other systems are touched.
- [x] **DINO-056** Sprint ban: `PlayerController` ignores the sprint input while `IsCarrying`; if the player is sprinting at the moment of pickup, the speed drops to walk at once. The roll and jump are not changed here.
- [x] **DINO-057** `PlayerNoiseEmitter`: no sprint-step noise while `IsCarrying` (check that DINO-056 already guarantees it, otherwise add a guard); landing noise is kept.
- [x] **DINO-058** `PlayerAnimator` publishes `IsCarrying` (bool) from `OnCarryStateChanged`; initial value is set on `Awake`; no animator or parameter → a warning and no exception.
- [x] **DINO-059** Carry animation (manual): the upper-body layer with an `AvatarMask` (arm and shoulder), the pose "logs lie on the shoulder, the hand holds them" for idle/walk, transitions by `IsCarrying`, the layer weight is blended smoothly; manual clip and Animator Controller setup — `manual-queue.md`.
- [x] **DINO-060** `PlayerLogCarrier.DropAll(DropReason reason)`: spawn all carried `LogItem`s at the positions of the visual slots (`CarriedLogsVisual`) with a small impulse in a random direction, then reset the counter. If the prefab is missing — a warning and the counter stays untouched (no logs lost). `DropReason` enum: `Roll`, `Jump`, `Manual`.
- [x] **DINO-061** Forced drop on roll: the roll start calls `DropAll(DropReason.Roll)` before the roll movement; the roll is never blocked by the drop result; i-frames from DINO-025 are unchanged.
- [x] **DINO-062** Forced drop on jump: the jump takeoff calls `DropAll(DropReason.Jump)`; a jump in the air is not repeated; the drop happens once per jump.
- [x] **DINO-063** Pickup lock after a forced drop: `PlayerLogPickup` ignores the logs for `PickupLockAfterDrop` seconds (config, `<= 0` → default), so that the player does not pick the fallen logs up in the same roll/jump; the manual pickup and `LogDropPointFinder` are not affected.
