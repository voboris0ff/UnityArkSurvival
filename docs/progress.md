# Текущий прогресс

Статус проекта в 40–60 строк. Подробности по задачам — `docs/session-logs/`, план — `docs/roadmap.md`, ручные шаги — `docs/manual-queue.md`.

## Статус
- Unity 6 / URP. Движение игрока готово полностью.
- Текущий этап: первое существо — наземный динозавр-хищник.
- Выполнено: DINO-001 … DINO-011. Следующая задача — **DINO-012** (доводка `Chase`).

## Игрок
- `CharacterController` + новый Input System; FSM Idle/Walk/Sprint/Jump/Fall/Roll.
- Jump buffer, coyote time, выносливость, приседание, камера от третьего лица.
- Animator-параметры публикуются через `PlayerAnimator`.
- Системы здоровья нет — контракт урона не согласован (DINO-021, ждёт ответа).

## Существо
| Задача | Компонент | Состояние |
| --- | --- | --- |
| DINO-002 | `CreatureConfig` | класс готов, ассет `Assets/Configs/CreatureConfig.asset` создан |
| DINO-003 | `CreatureNavigator` | готов, `IsPathActive` исправлен |
| DINO-004 | `CreatureController` | готов, `_homePosition` фиксируется в `Start` |
| DINO-005 | `CreatureStateMachine`, `IdleState` | готово |
| DINO-006 | `ReturnState`, `RequestReturn()` | работает, проверено в Play Mode |
| DINO-007 | `CreatureAnimator` (`Speed`) | публикует скорость; анимации движения не проверены |
| DINO-008 | `CreatureTargetMemory` | хранит цель/позицию/давность через `Tick(dt)` |
| DINO-009 | `CreatureVision` | дистанция/угол/raycast по `ObstacleLayerMask` |
| DINO-010 | `CreaturePerception` | периодический тик зрения по `PerceptionInterval` |
| DINO-011 | `ApplyPerception`, `InvestigateState`, `ChaseState` | зрение → память → Idle/Investigate/Chase; в Play Mode не проверено |

- Порядок кадра в `CreatureController.Update`: `Perception.Tick` → `Memory.Tick` → лог изменений → `ApplyPerception` (переходы) → `StateMachine.Tick`.
- Правила переходов (`ApplyPerception`): вижу цель → `Chase` (обновляет `Observe`); не вижу при живой памяти → `Investigate` (идёт к `LastKnownPosition`); память истекла → `Idle`. `Return` по-прежнему только по клавише `R`.
- `Investigate`/`Chase` — базовые версии: `Chase` идёт на `ChaseSpeed` и обновляет destination раз в `DestinationUpdateInterval`, `Investigate` один раз идёт к последней известной точке. Уточнение — DINO-012/DINO-013.
- Debug.Log: старт, каждое изменение «вижу/не вижу», «цель запомнена/память истекла», раз в секунду при невидимой, но живой памяти, каждый переход состояния.
- `CreatureTargetMemory`: `Observe(target, position)` обнуляет давность, `UpdatePosition` двигает позицию без сброса давности, `Clear` забывает всё; `HasTarget` = давность ≤ `MemoryDuration`.
- Ссылки вида `Target` могут стать `null` при уничтожении цели — `CreatureVision` и `ChaseState` это учитывают.
- NavMesh запечён (`Assets/Scenes/SampleScene/NavMesh-Plane.asset`), агент на сетке, возврат к домашней точке проезжает ~11 м.
- Отладочный компонент пользователя — `Assets/CreatureDebuger.cs` (`CreatureDebugInput`).

## Готовые соглашения
- Animator существа: параметр `Speed` (float, м/с), стейты `idle`/`walk`/`run`.
- Память о цели: создаётся в `CreatureController.Start` со сроком `CreatureConfig.MemoryDuration`; доступ — `Memory` и `CreatureContext.Memory`; тикается из `Update` каждый кадр.
- `CreatureVision`: `CanSee(target)` / `CanSee(target, out point)` — дистанция `SightDistance`, полный угол `SightAngle`, raycast по `ObstacleLayerMask`. Своих таймеров и решений нет.
- `CreatureContext` несёт `Config` — состояния берут скорости и интервалы из `CreatureConfig`.
- Зрение: цель — игрок (`Target` в инспекторе или автопоиск на слое `Player`); точка глаз — `Eye Point` либо `EyePoint` на `EyeHeight`.
- Слои: 3 `Creature`, 6 `Player`, 7 `Obstacle` (так в `TagManager.asset`).

## Ближайшие шаги
1. DINO-012: довести `Chase` (ограничение частоты destination, поведение при потере цели).
2. Пользователь: проверить в Play Mode обнаружение, память и переходы (пункт DINO-011 в очереди).
3. Пользователь: проверить анимации движения (DINO-007) и подобрать `PerceptionInterval` (DINO-010).

## Ограничения
- Агент не правит Unity-ассеты, не запускает сборку/тесты.
- Связь с пользователем — только через `manual-queue.md`.
