# Current Progress

Project status. Details — `docs/session-logs/`, plan — `docs/roadmap.md`, manual steps — `docs/manual-queue.md`, contracts — `docs/project-context.md`.

## Status

Unity 6 / URP; the first creature is ready and is being assembled in the Editor.

**Done / Выполнено:** DINO-001…043 (navigation, vision, memory, hearing, patrol, search, chase, attack, player death, turn control, prefab component list, camera near-wall fix, edge-case review, `TreeConfig`, `ChoppableTree`, `ChopConfig`, `PlayerChopInteractor` target search and swing, chop animation trigger and `OnChopHit` event, `LogItem`, `TreeLogSpawner`, `FelledTreeRemover`, tree prefab setup list, log spawn height setting).

**Next task / Следующая задача:** следующий открытый пункт `docs/roadmap.md`.

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
