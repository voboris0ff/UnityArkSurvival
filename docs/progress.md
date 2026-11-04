# Текущий прогресс

Статус проекта в 40–60 строк. Подробности по задачам — `docs/session-logs/`, план — `docs/roadmap.md`, ручные шаги — `docs/manual-queue.md`.

## Статус
- Unity 6 / URP. Движение игрока готово полностью.
- Текущий этап: первое существо — наземный динозавр-хищник.
- Выполнено: DINO-001 … DINO-023. Следующая задача — **DINO-024** (смерть игрока и контракт `IKillable`).

## Игрок
- `CharacterController` + новый Input System; FSM Idle/Walk/Sprint/Jump/Fall/Roll.
- Jump buffer, coyote time, выносливость, приседание, камера от третьего лица.
- Animator-параметры публикуются через `PlayerAnimator`.
- Системы здоровья нет — контракт урона не согласован (DINO-024).

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
| DINO-015 | `SearchState` | случайные точки вокруг последней известной позиции, паузы, лимит `SearchDuration` |
| DINO-016 | завершение неудачного поиска | `SetHome` на месте окончания погони, `Memory.Clear()`, Idle |
| DINO-017 | `CreaturePatrolPointSelector` | параметры `Patrol*` в конфиге, выбор достижимой точки вокруг дома |
| DINO-018 | `PatrolState` | Idle → Patrol, точки вокруг дома, паузы, сброс маршрута при смене дома |
| DINO-019 | `NoiseEvent`, `NoiseBroadcaster` | контракт шума: позиция, громкость 0..1, источник |
| DINO-020 | `CreatureHearing` | приём шума, дальность `HearingRange * Loudness`, подписка/отписка |
| DINO-021 | `ApplyNoise`, `RememberPoint` | шум → приблизительная точка в памяти → Investigate; цель видимой не становится |
| DINO-022 | `PlayerNoiseEmitter` | шум игрока: шаги бега и приземление через `NoiseBroadcaster`, `PlayerController` не менялся |
| DINO-023 | `ApplyNoise` (приоритеты) | зрение > шум; Chase/Return не прерываются; Search не сбивается шумом внутри области; Patrol/Idle/Investigate → осмотр точки шума |

- Порядок кадра в `CreatureController.Update`: `Perception.Tick` → `Memory.Tick` → лог изменений → `ApplyPerception` (переходы по зрению) → `ApplyNoise` (переходы по шуму) → `StateMachine.Tick`.
- Приоритеты зрения и слуха (DINO-023): видимая цель важнее шума; `Chase` и `Return` шум не прерывает; `Search` продолжается при шуме внутри `SearchRadius` от `SearchState.Anchor`, иначе осматривает новую точку; `Patrol`/`Idle`/`Investigate` переходят/перезапускают `Investigate`; шум ближе 3 м к осматриваемой точке только обновляет память.
- Правила переходов (`ApplyPerception`): вижу цель → `Chase`; не вижу при живой памяти и ещё не отработанном осмотре → `Investigate` (идёт к `LastKnownPosition`); осмотр отработан при живой памяти → `Search` не прерывается; память истекла → `Investigate`→`Search`, `Chase`→`Idle`. `Return` по-прежнему только по клавише `R`. `Observe` памяти выполняет `Chase` при видимой цели; `RequestInvestigate()` — команда осмотра из `Chase` при непостроенном пути.
- `Investigate` (DINO-013): идёт к `LastKnownPosition` на `MoveSpeed`, путь перестраивается только при заметном сдвиге точки; завершается прибытием (`HasArrived`), истечением `CreatureConfig.InvestigateDuration` (по умолчанию 8 с) или непостроенным/потерянным путём. По завершении вызывает `NotifyInvestigationFinished()` и переходит в `Search`: та же живая память больше не запускает осмотр, пока цель снова не будет видна.
- `Search` (DINO-015): центр поиска — `Memory.LastKnownPosition` на входе (не едет за существом); случайные достижимые точки в `SearchRadius` (по умолчанию 8 м, временное), пауза `SearchStopDuration` (1 с) на каждой, лимит `SearchDuration` (6 с). Восприятие не читает: в Chase переключает только `ApplyPerception`.
- `Search` завершение (DINO-016): по истечении `SearchDuration` без обнаружения цели — `Home.SetHome(Navigator.Position)` (место, где существо прекратило погоню), `Memory.Clear()`, `NotifySearchFinished()`, `Patrol` (DINO-018). Отдельного `Return` нет; `R` остаётся отладочной командой.
- `Patrol` (DINO-018): Idle без цели сразу ведёт в Patrol (если агент готов); скорость `PatrolSpeed`, точки из `CreaturePatrolPointSelector` вокруг `HomePosition`, по прибытии с шансом `PatrolWaitChance` пауза `Random.Range(PatrolWaitMin, PatrolWaitMax)`, нет точки — повтор через 0.5 с, смена дома > 0.05 м сбрасывает маршрут. Переходы по зрению — только `ApplyPerception`.
- `ApplyPerception` при истёкшей памяти: `Investigate` → `Search`, `Chase` → Idle; во время `Search` с живой памятью состояние не прерывается.
- `Chase` (DINO-012): идёт на `ChaseSpeed`, перестраивает путь только когда цель видна и сдвинулась дальше `MinRepathDistance` (и не чаще `DestinationUpdateInterval`), вблизи (< 3 м) точка назначения — на агенте; поиск вокруг точки — DINO-015.
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
1. DINO-024: изучить смерть/респавн игрока и описать контракт `IKillable` (без кода).
2. Пользователь: проверить приоритеты слуха (DINO-023) в Play Mode: шум не прерывает погоню/возврат, не сбивает поиск внутри его области, запускает осмотр из патруля.
3. Пользователь: проверить слух в Play Mode — клавиша `N` (`CreatureNoiseDebugInput`) публикует шум, в Console должны быть строки «услышал шум» только в пределах `HearingRange * Loudness`; затем проверить шум игрока (`PlayerNoiseEmitter`, DINO-022) — бег и приземление.
4. Пользователь: проверить патруль в Play Mode (DINO-018) — шаги, паузы, смену дома после неудачного поиска.
5. Пользователь: проверить анимации движения (DINO-007) и подобрать `PerceptionInterval` (DINO-010).
6. Пользователь: подобрать `PatrolRadius`/`PatrolSpeed`/паузы (DINO-017, значения подтверждены как стартовые).

## Ограничения
- Агент не правит Unity-ассеты, не запускает сборку/тесты.
- Связь с пользователем — только через `manual-queue.md`.
