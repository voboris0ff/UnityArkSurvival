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
| FSM | `ApplyPerception()` | единственная точка переходов по зрению/памяти: вижу → `Chase`, не вижу но помню → `Investigate`, память истекла → `Idle` | `CreatureStateMachine`, `CreatureController.Update` |

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
- `CreatureAnimator` (DINO-007) — публикует `Speed` из `NavMeshAgent.velocity`.
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
