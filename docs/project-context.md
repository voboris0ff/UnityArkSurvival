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
- `CreatureController` (DINO-004) — композиция, валидация, единственный тик FSM; `_homePosition` фиксируется в `Start`.
- `CreatureStateMachine` + `CreatureContext`, состояния `Idle` (DINO-005) и `Return` (DINO-006, проверено в Play Mode).
- `CreatureAnimator` (DINO-007) — публикует `Speed` из `NavMeshAgent.velocity`.
- Модели существ: `Assets/Art/Creatures/FBX/` (Raptor, Trex, Triceratops, Stegosaurus, Apatosaurus, Parasaurolophus); контроллер `Raptor.controller` содержит `idle`, клипы walk/run не подтверждены.
- Восприятия, памяти о цели, слуха и атаки нет.

## Приоритет этапов
1. Первое существо — наземный динозавр-хищник (текущий этап).
2. Интерфейс персонажа.
3. Карта: ресурсы, ландшафт, точки интереса.

Интерфейс и карта не начинаются без явного изменения приоритета пользователем. Декомпозиция — в `docs/roadmap.md`.
