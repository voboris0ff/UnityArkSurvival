# Текущий прогресс

Статус проекта в 40–60 строк. Подробности по задачам — `docs/session-logs/`, план — `docs/roadmap.md`, ручные шаги — `docs/manual-queue.md`.

## Статус
- Unity 6 / URP. Движение игрока готово полностью.
- Текущий этап: первое существо — наземный динозавр-хищник.
- Выполнено: DINO-001 … DINO-029. Следующая задача — **DINO-030** (список компонентов и ссылок префаба; префаб кодом не редактируется).

## Игрок
- `CharacterController` + новый Input System; FSM Idle/Walk/Sprint/Jump/Fall/Roll.
- Jump buffer, coyote time, выносливость, приседание, камера от третьего лица; параметры Animator — через `PlayerAnimator`.
- Системы здоровья и респавна нет. `PlayerController` реализует `IKillable`: любое попадание смертельно, i-frames кувырка дают `false`; после смерти — `IsDead`, остановка ввода/мотора, параметр Animator `Dead` (DINO-028). Респавн — решение пользователя.

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
| DINO-013 | `InvestigateState` (доводка) | осмотр точки до прибытия или `InvestigateDuration`, без повтора той же точки |
| DINO-015 | `SearchState` | случайные точки вокруг последней позиции, паузы, лимит `SearchDuration` |
| DINO-016 | завершение неудачного поиска | `SetHome` на месте окончания погони, `Memory.Clear()`, Idle |
| DINO-017 | `CreaturePatrolPointSelector` | параметры `Patrol*` в конфиге, выбор достижимой точки вокруг дома |
| DINO-018 | `PatrolState` | Idle → Patrol, точки вокруг дома, паузы, сброс маршрута при смене дома |
| DINO-019 | `NoiseEvent`, `NoiseBroadcaster` | контракт шума: позиция, громкость 0..1, источник |
| DINO-020 | `CreatureHearing` | приём шума, дальность `HearingRange * Loudness`, подписка/отписка |
| DINO-021 | `ApplyNoise`, `RememberPoint` | шум → приблизительная точка в памяти → Investigate; цель видимой не становится |
| DINO-022 | `PlayerNoiseEmitter` | шум игрока: шаги бега и приземление через `NoiseBroadcaster`, `PlayerController` не менялся |
| DINO-023 | `ApplyNoise` (приоритеты) | зрение > шум; Chase/Return не прерываются; Search не сбивается шумом внутри области |
| DINO-024 | контракт `IKillable` | описан (интерфейс + точка интеграции в `PlayerController`), код не менялся |
| DINO-025 | i-frames кувырка | `PlayerContext.IsInvulnerable` по таймеру `InvulnerabilityRemaining`; запуск в окне `IFramesStart`–`IFramesEnd`; подтверждено пользователем |
| DINO-026 | `CreatureAttack` | дистанция/сектор, замах, одна проверка попадания в `AttackHitTime`, пауза от конца замаха |
| DINO-027 | `AttackState` | старт по видимости + `CanStartAttack`, ограниченный доворот, после замаха → `Chase` |
| DINO-028 | смерть игрока | `ConsumeAttackHit` → `IKillable.TryKill`; одно попадание за замах; смерть → память сброшена, `Patrol`; мёртвая цель не видна |
| DINO-029 | анимация атаки | триггер `Attack` в `CreatureAnimator`, `CreatureAttackAnimationEvent.OnAttackHit` → `NotifyAnimationHit`; ручная настройка клипа — `manual-queue.md` |

- Порядок кадра `CreatureController.Update`: `Perception.Tick` → `Memory.Tick` → лог → `ApplyPerception` → `ApplyNoise` → `StateMachine.Tick` → `Attack.Tick` → `ConsumeAttackHit` → `TickRotation`.
- Переходы (`ApplyPerception`): вижу → `Chase`; не вижу при живой памяти → `Investigate` (к `LastKnownPosition`); осмотр отработан → `Search` не прерывается; память истекла → `Chase`→`Idle`. `Return` только по `R`. Приоритеты зрения/слуха (DINO-023): зрение > шум; `Chase`/`Return` не прерываются; шум внутри `SearchRadius` от `SearchState.Anchor` поиск не меняет.
- `Investigate` (DINO-013): скорость `MoveSpeed`, конец — прибытие, `InvestigateDuration` (8 с) или потерянный путь → `Search`. `Search` (DINO-015/016): точки в `SearchRadius` (8 м), пауза `SearchStopDuration` (1 с), лимит `SearchDuration` (6 с); по истечении — `SetHome`, `Memory.Clear()`, `Patrol`.
- `Patrol` (DINO-018): Idle без цели → Patrol; `PatrolSpeed`, точки вокруг `HomePosition`, пауза `PatrolWaitChance`/`PatrolWaitMin..Max`, нет точки — повтор через 0.5 с, смена дома > 0.05 м сбрасывает маршрут. `Chase` (DINO-012): `ChaseSpeed`, перестроение пути при смещении цели > `MinRepathDistance` (0.5 м) и не чаще `DestinationUpdateInterval`.
- Debug.Log: старт, «вижу/не вижу», «цель запомнена/память истекла», раз в секунду при живой памяти без зрения, каждый переход состояния. NavMesh запечён (`NavMesh-Plane.asset`), отладочный `CreatureDebugInput` — `Assets/CreatureDebuger.cs`.

## Готовые соглашения
- Animator существа: параметр `Speed` (float, м/с), стейты `idle`/`walk`/`run`; параметр `Attack` (trigger) и Animation Event `OnAttackHit` — DINO-029.
- Зрение: цель — игрок (`Target` или автопоиск на слое `Player`); точка глаз — `Eye Point` либо `EyePoint` на `EyeHeight`.
- Слои: 3 `Creature`, 6 `Player`, 7 `Obstacle` (так в `TagManager.asset`).

## Ближайшие шаги
1. Пользователь: настроить анимацию атаки в `Raptor.controller` (триггер `Attack`, стейт с клипом, Animation Event `OnAttackHit`, компонент `CreatureAttackAnimationEvent`) — пункт DINO-029 в `manual-queue.md`.
2. DINO-030: список обязательных компонентов и ссылок для префаба существа.
3. Пользователь: проверить приоритеты слуха (DINO-023) в Play Mode: шум не прерывает погоню/возврат и не сбивает поиск внутри его области.
4. Пользователь: проверить слух (`N`, `PlayerNoiseEmitter`) и патруль (DINO-018) в Play Mode.
5. Пользователь: проверить анимации движения (DINO-007) и подобрать `PerceptionInterval` (DINO-010), `PatrolRadius`/`PatrolSpeed`/паузы (DINO-017).

## Ограничения
- Агент не правит Unity-ассеты, не запускает сборку/тесты.
- Связь с пользователем — только через `manual-queue.md`.
