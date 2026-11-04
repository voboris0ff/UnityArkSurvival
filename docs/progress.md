# Текущий прогресс

Статус проекта в 40–60 строк. Подробности по задачам — `docs/session-logs/`, план — `docs/roadmap.md`, ручные шаги — `docs/manual-queue.md`.

## Статус
- Unity 6 / URP. Движение игрока готово полностью.
- Текущий этап: первое существо — наземный динозавр-хищник.
- Выполнено: DINO-001 … DINO-013. Следующая задача — **DINO-014** (`Search`).

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
| DINO-011 | `ApplyPerception`, `InvestigateState`, `ChaseState` | зрение → память → Idle/Investigate/Chase; проверено в Play Mode |
| DINO-012 | `ChaseState` (доводка) | путь только по заметному смещению цели и не чаще интервала |
| DINO-013 | `InvestigateState` (доводка) | осмотр точки до прибытия или `InvestigateDuration`, без повторного осмотра той же точки |

- Порядок кадра в `CreatureController.Update`: `Perception.Tick` → `Memory.Tick` → лог изменений → `ApplyPerception` (переходы) → `StateMachine.Tick`.
- Правила переходов (`ApplyPerception`): вижу цель → `Chase`; не вижу при живой памяти и ещё не отработанном осмотре → `Investigate` (идёт к `LastKnownPosition`); память истекла → `Idle`. `Return` по-прежнему только по клавише `R`. `Observe` памяти выполняет `Chase` при видимой цели; `RequestInvestigate()` — команда осмотра из `Chase` при непостроенном пути.
- `Investigate` (DINO-013): идёт к `LastKnownPosition` на `MoveSpeed`, путь перестраивается только при заметном сдвиге точки; завершается прибытием (`HasArrived`), истечением `CreatureConfig.InvestigateDuration` (по умолчанию 8 с) или непостроенным/потерянным путём. По завершении вызывает `NotifyInvestigationFinished()` — та же живая память больше не запускает осмотр, пока цель снова не будет видна.
- `Chase` (DINO-012): идёт на `ChaseSpeed`, перестраивает путь только когда цель видна и сдвинулась дальше `MinRepathDistance` (и не чаще `DestinationUpdateInterval`), вблизи (< 3 м) точка назначения — на агенте; осмотр вокруг точки — DINO-014.
- Debug.Log: старт, каждое изменение «вижу/не вижу», «цель запомнена/память истекла», раз в секунду при невидимой, но живой памяти, каждый переход состояния.
- `CreatureTargetMemory`: `Observe(target, position)` обнуляет давность, `UpdatePosition` двигает позицию без сброса давности, `Clear` забывает всё; `HasTarget` = давность ≤ `MemoryDuration`.
- Ссылки вида `Target` могут стать `null` при уничтожении цели — `CreatureVision` и `ChaseState` это учитывают.
- В `Chase` порог перестроения пути (`MinRepathDistance = 0.5` м) и дистанция «в упор» (`CloseRangeDistance = 3` м) — временные константы кода, подбираются пользователем.
- NavMesh запечён (`Assets/Scenes/SampleScene/NavMesh-Plane.asset`), агент на сетке, возврат к домашней точке проезжает ~11 м.
- Отладочный компонент пользователя — `Assets/CreatureDebuger.cs` (`CreatureDebugInput`).

## Готовые соглашения
- Animator существа: параметр `Speed` (float, м/с), стейты `idle`/`walk`/`run`.
- Память о цели: создаётся в `CreatureController.Start` со сроком `CreatureConfig.MemoryDuration`; доступ — `Memory` и `CreatureContext.Memory`; тикается из `Update` каждый кадр.
- `CreatureVision`: `CanSee(target)` / `CanSee(target, out point)` — дистанция `SightDistance`, полный угол `SightAngle`, raycast по `ObstacleLayerMask`. Своих таймеров и решений нет.
- `CreatureContext` несёт `Config` — состояния берут скорости и интервалы из `CreatureConfig`.
- `ChaseState` (DINO-012): путь ставится при входе и перестраивается только если цель видна, сместилась от прошлой точки назначения дальше `MinRepathDistance` и прошло `DestinationUpdateInterval`; в упор destination — на агенте; при целевом `null`/памяти без цели маршрут снимается, непостроенный путь → `RequestInvestigate()`.
- Зрение: цель — игрок (`Target` в инспекторе или автопоиск на слое `Player`); точка глаз — `Eye Point` либо `EyePoint` на `EyeHeight`.
- Слои: 3 `Creature`, 6 `Player`, 7 `Obstacle` (так в `TagManager.asset`).

## Ближайшие шаги
1. DINO-014: `Search` — ограниченный по времени поиск вокруг последней известной позиции.
2. Пользователь: проверить в Play Mode осмотр (пункт DINO-013 в очереди).
3. Пользователь: проверить анимации движения (DINO-007) и подобрать `PerceptionInterval` (DINO-010).

## Ограничения
- Агент не правит Unity-ассеты, не запускает сборку/тесты.
- Связь с пользователем — только через `manual-queue.md`.
