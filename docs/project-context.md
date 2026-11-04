# Project Context

Stable facts and contracts not visible from code; rules are in `AGENTS.md`.

## Technical Foundation

- Unity 6000.0.85f1 (Unity 6), URP 17.0.4, Input System 1.20.0, AI Navigation 2.0.15.
- Scripts in `Assets/Scripts/`; ScriptableObject configs in `Assets/Scripts/Data/`.
- Scenes, prefabs, Animators, layers and NavMesh are set up by the user; the agent only reads them.

## Naming Conventions (external contracts)

Names outside code only.

| Area | Name | Meaning |
| --- | --- | --- |
| Creature Animator | `Speed` | `float`, m/s, not normalized |
| Creature Animator | states `idle`, `walk`, `run` | 1D Blend Tree driven by `Speed` |
| Creature Animator | `Attack` trigger | sent once at windup start |
| Creature Animator | Event `OnAttackHit` | no arguments; `CreatureAttackAnimationEvent` on the Animator object |
| Player Animator | `Speed`, `Grounded`, `VerticalSpeed`, `Sprinting`, `Crouching`, `CrouchAmount`, `Roll`, `Dead` | `PlayerAnimator` writes them |
| Layers | `3 Creature`, `6 Player`, `7 Obstacle` | `ProjectSettings/TagManager.asset` |
| Vision | `Player` layer | default target if `_target` is empty |
| Vision | `Eye Point` | optional child; empty → point at `CreatureConfig.EyeHeight` |
| Vision | `SightAngle` | full angle in degrees, half to each side |
| Vision | `ObstacleLayerMask` | blocks sight; must include the target layer |
| Config | `PerceptionInterval` | vision tick period; `<= 0` means every frame |
| Hearing | `HearingRange` | `CreatureConfig` field; audible range is `HearingRange * Loudness` |
| Memory | `MemoryDuration`, `InvestigateDuration` | `CreatureConfig` fields; forget delay and investigation limit |
| Search | `SearchRadius`, `SearchStopDuration`, `SearchDuration` | tuning fields of `CreatureConfig` |
| Patrol | `PatrolRadius`, `PatrolSpeed`, `PatrolWaitChance`, `PatrolWaitMin`, `PatrolWaitMax` | tuning fields of `CreatureConfig` |
| Patrol | `CreaturePatrolPointSelector.TrySelectPoint` | reachable point around home via `CreatureContext.PatrolPoints` |
| Attack | `AttackRange`, `AttackAngle`, `AttackWindup`, `AttackHitTime`, `AttackCooldown` | `CreatureConfig`; `AttackAngle` is a full sector |
| Player noise | `_sprintLoudness`, `_sprintStepInterval`, `_landingLoudness`, `_landingMinFallSpeed` | `PlayerNoiseEmitter` fields |
| Player | i-frames | `IFramesStart`, `IFramesEnd` in `PlayerMovementConfig`; used by `RollState` |

## Frame Order (CreatureController.Update)

`CreaturePerception.Tick` → `CreatureTargetMemory.Tick` → `ApplyPerception` → `ApplyNoise` → `CreatureStateMachine.Tick` → `CreatureAttack.Tick` → hit consumption → `CreatureNavigator.TickRotation`.

Only `TickRotation` controls rotation; a hit fixed by `CreatureAttack` is applied in the same frame.

## ApplyPerception Transitions

```
Dead target    → memory cleared; Chase / Investigate / Search / Attack → Patrol
Windup running → no transition at all
Visible        → Attack if CreatureAttack.CanStartAttack, otherwise Chase
Remembered, investigation not spent yet → Investigate
Remembered, investigation already spent → Idle from Investigate / Chase / Attack
Memory expired → Search from Investigate, otherwise Idle from Chase / Attack
```

`RequestInvestigate()` (a chase with no route) is ignored during `Investigate`, `Return` and a windup.

## Sight and Hearing Priorities

- Sight beats hearing: with a visible target, noise changes nothing.
- A started windup is not cancelled by sight or noise; it finishes, then `AttackState` goes to `Chase`.
- `Chase` and `Return` are not interrupted by noise.
- `Search`: noise inside the area around `SearchState.Anchor` keeps the search; noise outside switches to `Investigate` of that point.
- `Patrol`, `Idle`, `Investigate`: noise starts `Investigate`; while investigating, a noise closer than `NoiseRetargetDistance` (`CreatureStateMachine`) only refreshes memory.
- Noise gives an approximate point only: `RememberPoint` stores it, the visible target is never replaced.

## Attack Contract

- A windup starts from `AttackState.Enter` only, when `CanStartAttack` allows: no windup, no cooldown, target visible and in range/sector; otherwise the state falls back to `Chase`.
- During the windup the creature stops and turns to the target with `TurnSpeed`.
- The windup is never cancelled by a state change: `ApplyPerception` and `ApplyNoise` return early while it runs.
- One hit check per windup, at `AttackHitTime` or the Animation Event, whichever is first; `NotifyAnimationHit` is ignored outside a windup and after the check.
- The creature takes the hit result once per windup; a miss or `false` from `TryKill` is never retried.
- `AttackCooldown` counts from the windup end, so the windup always plays fully; `Cancel` (target killed) drops it without cooldown.
- Damage is unused: an accepted hit is fatal.

## Death Contract (IKillable)

```csharp
public interface IKillable
{
    bool IsDead { get; }
    // source: attacker position.
    // true: hit accepted, target is now dead.
    // false: dead or invulnerable; never retry in this windup.
    bool TryKill(Vector3 source);
}
```

- `PlayerController`: `false` when `IsDead` or `PlayerContext.IsInvulnerable`; otherwise sets `IsDead`, publishes `Dead`, stops motor and input.
- `PlayerController.Update` exits early while `IsDead`: no FSM, motor or animation tick.
- Creature side: the hit consumer calls `TryKill(transform.position)` once; `true` → memory cleared, attack cancelled, → `Patrol`; `false` → log only.
- A destroyed target (object or `IKillable` component) counts as dead; an unassigned target is not dead.
- Perception never sees a dead target; `PlayerNoiseEmitter` is silent while dead. No health system: an accepted hit kills; post-death flow undefined.

## Hearing Pipeline

- `NoiseEvent`: `readonly struct` with `Position`, `Loudness` 0..1 (clamped) and `Source` (`Transform` or `null`).
- `NoiseBroadcaster.Emit` publishes to the static `Emitted` event; subscribers unsubscribe in `OnDisable`; the list resets on load.
- `CreatureHearing.Hear` accepts a noise when distance ≤ `HearingRange * Loudness` and the source is not the listener; `TryConsumeHeardNoise` hands it to `ApplyNoise` once.

## Initialization and Caching

- `CreatureController.Start` validates config and agent, resolves target and eye point, creates all subsystems and starts at `Idle`. Noise subscription is in `OnEnable`/`OnDisable`.
- `CreatureHome` is fixed at start from `_homePoint` or the start position; only `SetHome` changes it (a failed `Search` moves it).
- No `Find`/`GetComponent` in `Update`; all references are cached.

## Creature Prefab (mandatory components and references)

The prefab is assembled by hand; code only reads it.

| Object | Component | References |
| --- | --- | --- |
| Root, stands on the NavMesh | `NavMeshAgent` | none; taken in `Awake` if `_agent` is empty |
| Root | `CreatureController` | `_config` (required); `_agent`, `_homePoint`, `_eyePoint`, `_target` optional: auto agent, home = start position, eye = root + `EyeHeight`, target = first collider on `Player` layer within `max(50, SightDistance)` at start |
| Root, optional | `CreatureAnimator` | `_animator`, `_controller` — auto-found in children / on the same object |
| Animator object (model) | `Animator` | Controller with `Speed` (float), `Attack` (trigger), states `idle`/`walk`/`run`; `applyRootMotion` is forced off |
| Animator object | `CreatureAttackAnimationEvent` | `_controller` optional (parent lookup); Animation Event `OnAttackHit`, no arguments, at the hit frame |
| Debug object | `CreatureDebugInput` (`Assets/CreatureDebuger.cs`), `CreatureNoiseDebugInput` | `creature`, `_source` |

- The target needs the `Player` layer and a non-trigger `Collider`: `CreatureVision` aims at its colliders; a target without colliders is visible only through a `Renderer` fallback.
- `CreatureConfig.ObstacleLayerMask` must contain `Obstacle` (7); the default value `1` blocks nothing. If the target layer is also in the mask, the ray hits the target's own collider and the hit is accepted through `IsChildOf(target)`.
- The prefab must be placed on a baked NavMesh, otherwise `CreatureNavigator.IsReady` is false and no behaviour runs.

## Debug Tools

- `CreatureDebugInput` (`Assets/CreatureDebuger.cs`): `R` calls `RequestReturn`.
- New external names go here and need a `manual-queue.md` entry.

## User Decisions and Open Questions

1. Target of the first version is the player: perception is hardcoded to one target found on the `Player` layer.
2. Respawn is not defined: point of death, death screen and scene reload are user decisions.
3. Stage priority: land dino → character UI → map/exploration; no UI or map work without an explicit priority change.
