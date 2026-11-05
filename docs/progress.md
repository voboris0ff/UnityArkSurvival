# Current Progress

Project status. Details — `docs/session-logs/`, plan — `docs/roadmap.md`, manual steps — `docs/manual-queue.md`, contracts — `docs/project-context.md`.

## Status

Unity 6 / URP; the first creature is ready and is being assembled in the Editor.

**Done / Выполнено:** DINO-064 manual checklist for the carry layer transitions is queued for the user (roll/jump must not mix with the shoulder pose, layer weight blends out/in over ~0.2 s); DINO-063 `LogPickupConfig.PickupLockAfterDrop` (<= 0 → default) and `PlayerLogCarrier.OnForcedDrop(int)` raised by `DropAll` only for `Roll`/`Jump`; `PlayerLogPickup` clears its target and ignores pickup for that time, the manual drop and `LogDropPointFinder` are unaffected; DINO-062 `JumpState.Enter` calls `Carrier.DropAll(DropReason.Jump)` only after a successful `Motor.TryJump()`, before leaving the ground; an unsuccessful jump attempt (no ground/coyote) drops nothing, so the drop happens once per takeoff; DINO-061 `RollState.Enter` calls `Carrier.DropAll(DropReason.Roll)` before the roll movement, the drop result never blocks the roll, i-frames from DINO-025 are unchanged; DINO-060 `PlayerLogCarrier.DropAll(DropReason)` spawns the carried logs at their `CarriedLogsVisual` slots with a small random-direction impulse and resets the counter; a missing prefab or visuals logs a warning and keeps the counter untouched; `DropReason` enum (`Roll`, `Jump`, `Manual`), slot accessor `CarriedLogsVisual.GetSlot`; DINO-059 carry animation specified as a manual step: `CarryUpperBody.mask`, `Carry Upper Body` override layer, `Empty`/`Carry Locomotion` states with `Carry Idle`/`Carry Walk`, transitions by `IsCarrying` with ~0.2 s weight blend; `PlayerAnimator.SetCarrying` publishes `IsCarrying` from `OnCarryStateChanged`, the initial value comes from `PlayerLogCarrier.IsCarrying` at startup, missing animator or parameter logs a warning once; DINO-001…055 (navigation, vision, memory, hearing, patrol, search, chase, attack, player death, turn control, prefab component list, camera near-wall fix, edge-case review, `TreeConfig`, `ChoppableTree`, `ChopConfig`, `PlayerChopInteractor` target search and swing, chop animation trigger and `OnChopHit` event, `LogItem`, `TreeLogSpawner`, `FelledTreeRemover`, tree prefab setup list, log spawn height setting, `PlayerLogCarrier` counter and `AddLog()`/`RemoveLog()`/`OnCountChanged`, `LogCarryConfig` with `MaxCarriedLogs` and `PlayerLogCarrier.CanPickUp`/limit in `AddLog`, `LogPickupConfig` and `PlayerLogPickup` nearest-log search that skips carried, registered and destroyed logs, log pickup by `Pickup` input: `CanPickUp` and `AddLog()` first, the world log is destroyed only on success, `LogDropPointFinder` with a downward ground raycast, a free-space `OverlapSphere` check and fallback offsets, `PlayerLogDrop` spawns the log prefab and calls `RemoveLog()` only after a drop point is found, `CarriedLogsVisual` with fixed slots up to `MaxCarriedLogs` driven by `OnCountChanged`, stacking offsets and no colliders or Rigidbody on the visuals, `CarriedLogsVisual` anchor/prefab with a warning and no visuals when either is missing, `CarryAnchor` naming, `PlayerLogDrop` spawns beside the player at hand level with a forward impulse while the ground point stays only a landing check, static review of stages G and H with fixes: no pickup target or drop for a dead player, destroyed-tree guard in `TreeLogSpawner`/`FelledTreeRemover`, null-config guards, `PlayerController` integration points mapped: sprint input/entry/exit, roll start and i-frames, jump takeoff and landing, `PlayerAnimator` parameters, `PlayerNoiseEmitter` triggers, `PlayerLogCarrier.IsCarrying` and `OnCarryStateChanged` raised only on the `0 ↔ >0` transition, sprint ban while carrying: `PlayerContext.SprintHeld` gates the input and `CharacterMotor.ClampSpeed` drops speed to walk on pickup, `PlayerNoiseEmitter` does not emit sprint-step noise while `IsCarrying`).

**Next task / Следующая задача:** DINO-064 — waiting for the user's manual check of the carry layer transitions (item in `manual-queue.md`).

## Systems

- **Player:** CharacterController, FSM (Idle/Walk/Sprint/Jump/Fall/Roll), camera, roll i-frames, `IKillable` interface, death without respawn.
- **Creature — navigation:** NavMeshAgent, `CreatureNavigator`, `CreatureController`, `CreatureConfig`.
- **Creature — vision:** distance, angle, Obstacle-layer raycast, periodic tick, target memory.
- **Creature — memory:** LastKnownPosition, forget delay, clearing.
- **Creature — hearing:** range depends on loudness, own noise ignored, conversion to a remembered point.
- **Creature — patrol:** points around the home position, pauses, reset when home changes.
- **Creature — search:** random points in a radius, pauses, switch to patrol on timeout.
- **Creature — chase:** path rebuild on noticeable target shift, no more often than the interval.
- **Creature — attack:** distance and sector check, windup, one hit check, pause, hit calls `IKillable.TryKill`.
- **Player death:** early exit from `PlayerController.Update`, creature memory reset, switch to patrol.

## Links

- [Roadmap](roadmap.md)
- [Project Context](project-context.md)
- [Manual Queue](manual-queue.md)
- [Session Logs](session-logs/)
