# Контекст проекта

Правила работы — только в `AGENTS.md`. Здесь стабильные факты и соглашения.

## Техническая основа
- Unity 6000.0.85f1 (Unity 6), URP 17.0.4, Input System 1.20.0, AI Navigation 2.0.15.
- Скрипты — `Assets/Scripts/`; ScriptableObject-конфиги — `Assets/Scripts/Data/`.
- Unity-ассеты (сцены, префабы, Animator, слои, NavMesh) настраивает пользователь; агент их только читает для анализа.

## Соглашения об именах

| Область | Имя | Тип / смысл | Где используется |
| --- | --- | --- | --- |
| Animator существа | `Speed` | `float`, м/с, без нормализации | `CreatureAnimator` → `Raptor.controller` |
| Animator существа | стейты `idle` / `walk` / `run` | Blend Tree 1D по `Speed` | `Raptor.controller` |
| Animator игрока | `Speed`, `Grounded`, `VerticalSpeed`, `Sprinting`, `Crouching`, `CrouchAmount`, `Roll` | как в `PlayerAnimator` | `Player.controller` |
| Animator существа | `Attack` | `trigger`, ставится один раз на старт замаха (`CreatureAttack.IsAttacking` false → true); имя выбрано агентом при пустом `Raptor.controller` | `CreatureAnimator` → `Raptor.controller` (DINO-029) |
| Animator существа | Animation Event `OnAttackHit()` | приёмник на том же GameObject, что Animator, без аргументов; вызывает `CreatureAttack.NotifyAnimationHit()` | `CreatureAttackAnimationEvent` (DINO-029) |
| Creatures | namespace `ArkSurvival.AI.Creatures` | состояния — `.CreatureStates` | `Assets/Scripts/AI/Creatures/` |
| Data | namespace `ArkSurvival.Data`, меню `ArkSurvival/<Name> Config` | `sealed` ScriptableObject | `Assets/Scripts/Data/` |
| Зрение | цель первой версии — `Player` (слой `Player`, компонент `PlayerController`) | `Transform` в `CreatureController.Target`; пусто → автопоиск на слое `Player` при старте | `CreatureVision`, `CreatureController` |
| Зрение | точка глаз | `Eye Point` (`CreatureController._eyePoint`); пусто → дочерний `EyePoint` на высоте `CreatureConfig.EyeHeight` | `CreatureVision` |
| Зрение | `SightAngle` — полный угол обзора | градусы; 110° = ±55° от направления вперёд | `CreatureConfig`, `CreatureVision` |
| Зрение | препятствия | `ObstacleLayerMask`; цель должна быть на слое `Player` (иначе её коллайдер сам может перекрывать луч) | `CreatureConfig`, `CreatureController` |
| Слои | `3 Creature`, `6 Player`, `7 Obstacle` | имена из `ProjectSettings/TagManager.asset` | сцена, маски |
| Восприятие | `PerceptionInterval` | период тика зрения, с; `<= 0` — каждый кадр | `CreatureConfig`, `CreaturePerception` |
| Память | `Tick(dt)` | вызывается каждый кадр; давность (`TimeSinceLastSeen`) считается накоплением `dt`, а не `Time.time` | `CreatureTargetMemory`, `CreatureController.Update` |
| FSM | `ApplyPerception()` | единственная точка переходов по зрению/памяти: вижу → `Chase`, не вижу но помню → `Investigate`, память истекла → `Idle`; `Idle` без цели сразу ведёт в `Patrol` | `CreatureStateMachine`, `CreatureController.Update` |
| FSM | `RequestInvestigate()` | команда осмотра последней известной точки (используется `Chase` при непостроенном пути) | `CreatureStateMachine` |
| Поиск | `SearchRadius`, `SearchStopDuration` | 8 м и 1 с, временные: радиус случайных точек вокруг последней известной позиции и пауза на точке | `CreatureConfig`, `SearchState` |
| Поиск | `NotifySearchFinished()` | конец поиска без обнаружения цели; домашняя точка уже перенесена на место окончания погони, память очищена (DINO-016) | `CreatureStateMachine`, `SearchState` |
| Поиск | `SearchState.Finish` | неудачный поиск: `Home.SetHome(Navigator.Position)` → `Memory.Clear()` → `NotifySearchFinished()` → `Patrol` (DINO-018) | `SearchState` |
| Погоня | `MinRepathDistance`, `CloseRangeDistance` | 0.5 м и 3 м, временные константы `ChaseState`: порог перестроения пути и дистанция «в упор» | `ChaseState` |
| Патруль | `PatrolRadius`, `PatrolSpeed` | 10 м и 2 м/с, временные: радиус точек вокруг домашней точки и скорость обхода | `CreatureConfig`, `CreaturePatrolPointSelector` |
| Патруль | `PatrolWaitChance`, `PatrolWaitMin`, `PatrolWaitMax` | 0.5 и 1–3 с, временные: шанс паузы на точке и её длительность | `CreatureConfig`, `PatrolState` (DINO-018) |
| Патруль | `CreaturePatrolPointSelector.TrySelectPoint(out Vector3)` | выбор случайной достижимой точки: `NavMesh.SamplePosition` + `CreatureNavigator.CanReach`, не ближе 2 м, до 10 попыток; доступ через `CreatureContext.PatrolPoints` | `CreaturePatrolPointSelector` |
| Патруль | `PatrolState` | Idle без цели → Patrol; скорость `PatrolSpeed`, пауза на точке с шансом `PatrolWaitChance` на `Random.Range(PatrolWaitMin, PatrolWaitMax)`; нет точки — повтор через 0.5 с; смена дома > 0.05 м сбрасывает маршрут (DINO-018) | `PatrolState`, `CreatureStateMachine` |
| Слух | namespace `ArkSurvival.AI.Perception` | контракт шума (DINO-019): `NoiseEvent`, `NoiseBroadcaster` | `Assets/Scripts/AI/Perception/` |
| Слух | `NoiseEvent(position, loudness, source)` | `readonly struct`: `Position` (точка шума), `Loudness` 0..1 (обрезается), `Source` (`Transform`, допустим `null`), `HasSource`, `SourceName` | `NoiseEvent` |
| Слух | `NoiseBroadcaster.Emit(...)` / `Emitted` | статический ретранслятор: `Emit(NoiseEvent)` или `Emit(position, loudness, source)`, подписка через `event Action<NoiseEvent> Emitted`; слышимость не считает | `NoiseBroadcaster` |
| Слух | `CreatureHearing.Hear(NoiseEvent)` | приём шума (DINO-020): слышно, если дистанция ≤ `HearingRange * Loudness`; собственный шум (источник — слушатель или его потомок) игнорируется; хранит последнее событие (`LastNoise`, `LastHeardPosition`, `LastDistance`) | `CreatureHearing` |
| Слух | `CreatureController.Hearing`, `CreatureContext.Hearing` | доступ к слуху; память и переходы по шуму — DINO-021 | `CreatureController`, `CreatureContext` |
| Слух | `CreatureNoiseDebugInput` | временный отладочный издатель шума по клавише `N` для проверки в Play Mode (можно удалить, когда ручные проверки DINO-020/021 закрыты) | `CreatureNoiseDebugInput` |
| Слух | `PlayerNoiseEmitter` | шум игрока (DINO-022): шаги бега — `_sprintLoudness = 0.7` каждые `_sprintStepInterval = 0.35` с в состоянии `Sprint`; приземление — `_landingLoudness = 1.0` при скорости падения ≥ `_landingMinFallSpeed = 3` м/с; позиция и источник — Transform игрока. Временные значения, подбираются вручную | `PlayerNoiseEmitter` |
| Слух | `CreatureStateMachine.NoiseRetargetDistance` | 3 м, временное: шум ближе этого к осматриваемой точке не перезапускает осмотр (DINO-023) | `CreatureStateMachine` |
| Слух | `SearchState.Anchor` | опора текущего поиска (последняя известная позиция на входе); по ней определяется «шум внутри области поиска» (DINO-023) | `SearchState`, `CreatureStateMachine` |
| Смерть | `ArkSurvival.Combat.IKillable` | интерфейс цели, которую можно убить одним попаданием; namespace по папке `Assets/Scripts/Combat/` | `CreatureController` → `PlayerController` |
| Смерть | `IKillable.TryKill(Vector3 source)` | `source` — позиция атакующего (для ориентира/импульса); `true` — попадание принято, цель мертва; `false` — неуязвима (i-frames) или уже мертва, повторять попытку в этом замахе нельзя | контракт DINO-024, реализация — DINO-028 |
| Смерть | `PlayerController.IsDead`, `PlayerController.TryKill` | игрок реализует `IKillable`: `IsDead` — публичный флаг, `TryKill` проверяет `_context.IsInvulnerable` и `IsDead` | точка интеграции DINO-024 |
| Смерть | `PlayerContext.IsInvulnerable` | `InvulnerabilityRemaining > 0`; таймер уменьшается в `PlayerContext.Tick`; запуск — `StartInvulnerability(duration)`, сброс — `ClearInvulnerability()` (DINO-025) | `PlayerContext`, `RollState` |
| Смерть | окно i-frames кувырка | `IFramesStart`–`IFramesEnd` в `PlayerMovementConfig` (0.05–0.35 с, временные): `RollState` один раз за кувырок запускает таймер длиной `IFramesEnd - IFramesStart` | `PlayerMovementConfig`, `RollState` |
| Атака | `CreatureAttack` | класс ближней атаки (DINO-026): `IsTargetInAttackArea(Transform)` (горизонтальная дистанция `AttackRange`, сектор `AttackAngle`/2, допуск по высоте 1.5 м), `CanStartAttack`, `TryStartAttack`, `Tick(dt)`, `TryConsumeHit(out Transform)`, `Cancel()`; поля `IsAttacking`, `Target`, `CooldownRemaining`, `IsOnCooldown`, `HasPendingHit` | `CreatureAttack`, `CreatureContext.Attack` |
| Атака | `AttackHitTime` | 0.25 с, временное: момент единственной проверки попадания внутри замаха; обрезается длительностью `AttackWindup` | `CreatureConfig`, `CreatureAttack` |
| Атака | пауза от конца замаха | `AttackCooldown` (~1 с) отсчитывается после полного проигрывания замаха, поэтому начатый замах всегда доигрывается | `CreatureAttack`, `CreatureController.Update` |
| Атака | тик | `CreatureAttack.Tick` вызывается каждый кадр в `CreatureController.Update` (после `StateMachine.Tick`), независимо от состояния | `CreatureController` |
| Атака | `AttackState` | состояние атаки (DINO-027): вход только по `Perception.TargetVisible` + `CreatureAttack.TryStartAttack`; на замахе `Navigator.Stop()`, доворот к цели с ограничением `CreatureConfig.TurnSpeed` (`Quaternion.RotateTowards`), память освежается каждый кадр; конец замаха (`!IsAttacking`) → `Chase`; условия не выполнены на входе → `Chase` без замаха | `AttackState`, `CreatureStateMachine.Attack` |
| Атака | `CreatureStateMachine.IsAttacking` | `Context.Attack.IsAttacking`; пока замах идёт, `ApplyPerception`, `ApplyNoise`, `RequestInvestigate`, `RequestReturn` поведение не меняют | `CreatureStateMachine` |
| Смерть | Animator игрока `Dead` | `bool`, включается один раз при смерти (имя выбрано агентом; параметр и клип добавляет пользователь в `Player.controller`) | `PlayerAnimator` (DINO-028) |
| Смерть | `CreatureController.ConsumeAttackHit()` | в том же кадре после `CreatureAttack.Tick` забирает `TryConsumeHit` и вызывает `IKillable.TryKill(transform.position)` ровно один раз за замах; `false` — лог «не принято», повтора нет; `true` → `CreatureStateMachine.NotifyTargetKilled()` (DINO-028) | `CreatureController` |
| Смерть | `CreaturePerception.TargetIsDead` | `IKillable.IsDead` цели, найденной через `GetComponentInParent` при создании; мёртвая цель в `Refresh` никогда не считается видимой | `CreaturePerception` |
| Смерть | `CreatureStateMachine.ApplyPerception` (мёртвая цель) | проверка `TargetIsDead` стоит **до** правила «замах не отменяется»: память очищается, `Chase`/`Investigate`/`Search`/`Attack` → `Patrol` | `CreatureStateMachine` |
| Смерть | `CreatureStateMachine.NotifyTargetKilled()` | после принятого `TryKill`: `Memory.Clear()`, `Attack.Cancel()`, `_wasPursuing = false`, `_investigationSpent = true`, `Patrol` | `CreatureStateMachine`, `CreatureController` |
| Смерть | `PlayerNoiseEmitter` при смерти | при `PlayerController.IsDead` шум не издаётся (иначе застывшее состояние Sprint продолжало бы «шагать») | `PlayerNoiseEmitter` |

## Отладочные средства
- `Assets/CreatureDebuger.cs` — компонент `CreatureDebugInput` (класс назван иначе, чем файл): по клавише `R` вызывает `CreatureController.RequestReturn()`.

Новое имя добавляется сюда в той же сессии, где появилось, и сопровождается пунктом в `manual-queue.md`.

## Приоритеты зрения и слуха (DINO-023)
- Зрение всегда важнее слуха: если цель видна, услышанный шум не меняет поведение (только лог).
- `Chase` шумом не прерывается — погоня за видимой/помнимой целью имеет приоритет.
- `Return` (по команде `R`) шумом не отменяется.
- `Search`: шум внутри области поиска (`SearchRadius` вокруг `SearchState.Anchor`) поиск не меняет; шум снаружи переключает на `Investigate` его точки.
- `Patrol`, `Idle`, `Investigate`: шум запускает `Investigate` к приблизительной точке; в `Investigate` шум ближе `NoiseRetargetDistance` (3 м, временное) только обновляет память.
- Порядок кадра: `ApplyPerception` → `ApplyNoise` → `StateMachine.Tick`.
- Начатый замах (DINO-027): `ApplyPerception` и `ApplyNoise` при `IsAttacking` поведение не меняют; замах доигрывается, после него `AttackState` → `Chase`.

## Реализовано

### Контракт смерти игрока (DINO-024)

Состояние вопроса на конец DINO-024:
- Системы здоровья нет. Любое попадание смертельно; повторной попытки в том же замахе быть не должно.
- Респавна нет: в скриптах нет ни `Spawn`, ни `Respawn`, ни `SetActive(false)` для игрока. Точка возрождения, экран смерти и загрузка сцены — решения пользователя.
- Кувырок (`RollState`) запускает таймер неуязвимости `PlayerContext.StartInvulnerability` в окне `IFramesStart`–`IFramesEnd` (`PlayerMovementConfig`), один раз за кувырок; `IsInvulnerable` — `InvulnerabilityRemaining > 0`, таймер тикает в `PlayerContext.Tick` (DINO-025). Для `TryKill` это готовая проверка неуязвимости.

Контракт (описан, код не менялся):

```csharp
namespace ArkSurvival.Combat
{
    public interface IKillable
    {
        // source — позиция атакующего. true — попадание принято (цель мертва);
        // false — цель неуязвима (i-frames) или уже мертва.
        bool TryKill(Vector3 source);
    }
}
```

Правила:
- Вызывает только атакующий в момент `HitTime` (DINO-028), один раз за замах.
- `false` не повторяется в этом замахе и не считается промахом для логики cooldown.
- `true` — цель мертва: игрок обездвиживается, память существа о нём сбрасывается, существо не атакует мёртвую цель.

Точка интеграции в `PlayerController`:
- `public sealed class PlayerController : MonoBehaviour, IKillable`.
- Поле `public bool IsDead { get; private set; }`.
- `TryKill(Vector3 source)`: `if (IsDead || _context == null || _context.IsInvulnerable) return false;` → `IsDead = true`, сброс скорости/ввода, опубликовать `Dead` в Animator, вернуть `true`.
- `Update()` начинается с `if (IsDead) return;` (после `UpdateCursorLock`) — FSM, мотор и аниматор движения не тикают.
- `Death`/`Respawn` как отдельные состояния FSM не вводятся, пока пользователь не определит поведение после смерти.

### Игрок
- `PlayerController`, `PlayerInputReader`, `CharacterMotor`; FSM: Idle, Walk, Sprint, Jump, Fall, Roll.
- Jump buffer, coyote time, выносливость, приседание отдельным слоем, камера от третьего лица.
- `PlayerAnimator` публикует параметры в Animator. Игрок — layer 6 в `SampleScene`, системы здоровья нет.
- `PlayerNoiseEmitter` (DINO-022) — шум игрока: шаги бега (= Sprint, 0.7 каждые 0.35 с) и приземление (= падение ≥ 3 м/с, 1.0) через `NoiseBroadcaster.Emit`. `PlayerController` не менялся; значения временные.

### Существо (`Assets/Scripts/AI/Creatures/`)
- `CreatureConfig` (DINO-002) — параметры навигации, восприятия, памяти и атаки; ассет `Assets/Configs/CreatureConfig.asset`.
- `CreatureNavigator` (DINO-003) — адаптер над `NavMeshAgent`: destination, скорость, стоп, проверка достижимости. `IsPathActive` учитывает `pathPending` до `hasPath`.
- `CreatureController` (DINO-004) — композиция, валидация, единственный тик FSM и восприятия; `_homePosition` фиксируется в `Start`.
- `CreatureStateMachine` + `CreatureContext`, состояния `Idle` (DINO-005), `Return` (DINO-006, проверено в Play Mode), `Investigate` и `Chase` (DINO-011).
- `CreatureStateMachine.ApplyPerception` (DINO-011) — тик восприятия обновляет память (`Observe` при виде), затем машина переключает `Chase` (вижу) / `Investigate` (не вижу, но помню) / `Idle` (память истекла).
- `ChaseState` (DINO-012) — погоня с экономным перестроением пути: только по заметному смещению видимой цели и не чаще `DestinationUpdateInterval`; `RequestInvestigate()` в машине.
- `SearchState` (DINO-015) — поиск после осмотра: центр — `LastKnownPosition` на входе, случайные достижимые точки в `SearchRadius`, пауза `SearchStopDuration`, лимит `SearchDuration`; переход в `Chase` только через `ApplyPerception`.
- Завершение неудачной погони (DINO-016) — по истечении поиска `SearchState` переносит домашнюю точку на текущую позицию существа (`SetHome`), сбрасывает память (`Clear`) и уходит в `Idle`; отдельного состояния `Return` не добавлено.
- `CreaturePatrolPointSelector` (DINO-017) — выбор случайной достижимой точки патруля вокруг `HomePosition` (`TrySelectPoint`).
- `PatrolState` (DINO-018) — свободное патрулирование вокруг домашней точки: `Idle` без цели сразу ведёт в `Patrol`, точка из `CreaturePatrolPointSelector`, паузы по `PatrolWaitChance`/`PatrolWaitMin`–`Max`, повтор выбора через 0.5 с, маршрут сбрасывается при смене дома; `SearchState.Finish` теперь ведёт в `Patrol`.
- `CreatureAnimator` (DINO-007) — публикует `Speed` из `NavMeshAgent.velocity`.
- `CreatureAttack` (DINO-026) — ближняя атака: геометрия цели (дистанция `AttackRange`, сектор `AttackAngle`, допуск по высоте 1.5 м), замах `AttackWindup`, ровно одна проверка попадания в `AttackHitTime`, пауза `AttackCooldown` от конца замаха; результат забирается через `TryConsumeHit`. С DINO-028 попадание применяется к цели через `IKillable.TryKill` в `CreatureController.ConsumeAttackHit`. Доступ через `CreatureContext.Attack`; тикается в `CreatureController.Update`.
- Контракт шума (DINO-019) — `AI/Perception`: `NoiseEvent` (позиция, громкость 0..1, источник) и `NoiseBroadcaster` (`Emit`/`Emitted`).
- `CreatureHearing` (DINO-020) — приём шума: слышимость по дистанции и `HearingRange * Loudness`, собственный шум игнорируется, хранится последнее услышанное событие; подписка в `CreatureController.Start`, отписка в `OnDisable`. Переходов по шуму нет (DINO-021). Отладочный издатель — `CreatureNoiseDebugInput` (клавиша `N`).
- `CreatureTargetMemory` (DINO-008) — хранилище цели: `Observe`, `UpdatePosition`, `Clear`, `HasTarget`/`TimeSinceLastSeen` по `MemoryDuration`; решения не принимает. Доступ через `CreatureContext.Memory`. С DINO-011 давность накапливается `Tick(dt)`, который вызывает `CreatureController.Update`.
- `CreatureVision` (DINO-009) — проверка зрения: дистанция, угол обзора, raycast до коллайдера цели по `ObstacleLayerMask` (триггеры игнорируются). Возвращает точку реальной видимости; память не трогает. Доступ через `CreatureContext.Vision`.
- `CreaturePerception` (DINO-010) — периодический тик зрения: `CanSee` вызывается раз в `PerceptionInterval` (`Tick(dt)` каждый кадр, Physics — по интервалу; `<= 0` — каждый кадр). Результат последней проверки: `HasResult`, `TargetVisible`, `VisiblePoint`; `Refresh()` — внеплановая проверка, `Reset()` — забыть. Память не меняет, решений не принимает. Доступ через `CreatureContext.Perception`; тикается в `CreatureController.Update`.
- Просветки существа: `_target` и `_eyePoint` в `CreatureController`.
- `AttackState` (DINO-027) — состояние ближней атаки: старт только при видимой цели и `CanStartAttack`, иначе сразу `Chase`; на замахе стоит на месте и доворачивается к цели с лимитом `TurnSpeed`; конец замаха → `Chase`. Машина не прерывает начатый замах восприятием/шумом; `Attack` без активного замаха при истёкшей памяти уходит в `Idle`.
- Смерть игрока (DINO-028) — `CreatureController.ConsumeAttackHit` применяет единственное попадание замаха: `IKillable.TryKill(transform.position)`; `true` → `NotifyTargetKilled` (память очищена, замах/пауза сняты, `Patrol`); `false` (i-frames/уже мертва) → только лог, повтор в этом замахе не выполняется. Мёртвая цель никогда не видна (`CreaturePerception.TargetIsDead`), поэтому повторного захвата нет; `PlayerNoiseEmitter` мёртвого игрока молчит. Второй системы здоровья/урона каждый кадр нет.
- Модели существ: `Assets/Art/Creatures/FBX/` (Raptor, Trex, Triceratops, Stegosaurus, Apatosaurus, Parasaurolophus); контроллер `Raptor.controller` содержит `idle`, клипы walk/run не подтверждены.
- Восприятие и память (DINO-011): зрение → `Observe` в память → `Chase`; пропажа из виду при живой памяти → `Investigate`; истечение памяти → `Idle`. Атака — `CreatureAttack` (DINO-026, без урона), состояние `Attack` — DINO-027.

## Приоритет этапов
1. Первое существо — наземный динозавр-хищник (текущий этап).
2. Интерфейс персонажа.
3. Карта: ресурсы, ландшафт, точки интереса.

Интерфейс и карта не начинаются без явного изменения приоритета пользователем. Декомпозиция — в `docs/roadmap.md`.
