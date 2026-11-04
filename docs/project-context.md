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
| Слух | `NoiseBroadcaster.Emit(...)` / `Emitted` | статический ретранслятор: `Emit(NoiseEvent)` или `Emit(position, loudness, source)`, подписка через `event Action<NoiseEvent> Emitted`; слышимость не считает (DINO-020) | `NoiseBroadcaster` |

## Отладочные средства
- `Assets/CreatureDebuger.cs` — компонент `CreatureDebugInput` (класс назван иначе, чем файл): по клавише `R` вызывает `CreatureController.RequestReturn()`.

Новое имя добавляется сюда в той же сессии, где появилось, и сопровождается пунктом в `manual-queue.md`.

## Реализовано

### Игрок
- `PlayerController`, `PlayerInputReader`, `CharacterMotor`; FSM: Idle, Walk, Sprint, Jump, Fall, Roll.
- Jump buffer, coyote time, выносливость, приседание отдельным слоем, камера от третьего лица.
- `PlayerAnimator` публикует параметры в Animator. Игрок — layer 6 в `SampleScene`, системы здоровья нет.

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
- Контракт шума (DINO-019) — `AI/Perception`: `NoiseEvent` (позиция, громкость 0..1, источник) и `NoiseBroadcaster` (`Emit`/`Emitted`); слушателей, дальности и интеграции нет.
- `CreatureTargetMemory` (DINO-008) — хранилище цели: `Observe`, `UpdatePosition`, `Clear`, `HasTarget`/`TimeSinceLastSeen` по `MemoryDuration`; решения не принимает. Доступ через `CreatureContext.Memory`. С DINO-011 давность накапливается `Tick(dt)`, который вызывает `CreatureController.Update`.
- `CreatureVision` (DINO-009) — проверка зрения: дистанция, угол обзора, raycast до коллайдера цели по `ObstacleLayerMask` (триггеры игнорируются). Возвращает точку реальной видимости; память не трогает. Доступ через `CreatureContext.Vision`.
- `CreaturePerception` (DINO-010) — периодический тик зрения: `CanSee` вызывается раз в `PerceptionInterval` (`Tick(dt)` каждый кадр, Physics — по интервалу; `<= 0` — каждый кадр). Результат последней проверки: `HasResult`, `TargetVisible`, `VisiblePoint`; `Refresh()` — внеплановая проверка, `Reset()` — забыть. Память не меняет, решений не принимает. Доступ через `CreatureContext.Perception`; тикается в `CreatureController.Update`.
- Просветки существа: `_target` и `_eyePoint` в `CreatureController`.
- Модели существ: `Assets/Art/Creatures/FBX/` (Raptor, Trex, Triceratops, Stegosaurus, Apatosaurus, Parasaurolophus); контроллер `Raptor.controller` содержит `idle`, клипы walk/run не подтверждены.
- Восприятие и память (DINO-011): зрение → `Observe` в память → `Chase`; пропажа из виду при живой памяти → `Investigate`; истечение памяти → `Idle`. Слуха и атаки нет (DINO-016+).

## Приоритет этапов
1. Первое существо — наземный динозавр-хищник (текущий этап).
2. Интерфейс персонажа.
3. Карта: ресурсы, ландшафт, точки интереса.

Интерфейс и карта не начинаются без явного изменения приоритета пользователем. Декомпозиция — в `docs/roadmap.md`.
