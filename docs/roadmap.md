# Roadmap

Статусы: `[ ]` — ожидает выполнения, `[x]` — выполнено, `[!]` — ждёт ответа пользователя в `manual-queue.md`.
Одна задача = одна сессия OpenCode. Не брать следующую задачу в рамках той же сессии.
Заблокированные (`[!]`) задачи не останавливают работу: агент берёт следующую `[ ]`.

## Этап A. Первое существо: базовая архитектура и навигация

- [x] **DINO-001** контракт `CreatureController`.
- [x] **DINO-002** `CreatureConfig` (ScriptableObject-класс; `.asset` создаёт пользователь).
- [x] **DINO-003** `CreatureNavigator` поверх NavMeshAgent (цель, стоп, скорость, путь, безопасные проверки).
- [x] **DINO-004** `CreatureController` — композиция ссылок и валидация.
- [x] **DINO-005** `CreatureStateMachine` + `IdleState`.
- [x] **DINO-006** `ReturnState`, `RequestReturn()`; проверено в Play Mode (`R`).
- [x] **DINO-007** `CreatureAnimator` публикует `Speed` (float); блендтри `walk`/`run` настраивает пользователь.

## Этап B. Зрение, память о цели и потеря игрока

- [x] **DINO-008** `CreatureTargetMemory`: цель, `LastKnownPosition`, давность, `Clear`.
- [x] **DINO-009** `CreatureVision`: дистанция, угол обзора, raycast по `ObstacleLayerMask`.
- [x] **DINO-010** `CreaturePerception`: периодический тик зрения (`PerceptionInterval`).
- [x] **DINO-011** `ApplyPerception` — единственная точка переходов: вижу → `Chase`, помню → `Investigate`, истекло → `Idle`.
- [x] **DINO-012** `ChaseState`: путь не чаще `DestinationUpdateInterval` и только при смещении цели > `MinRepathDistance`.
- [x] **DINO-013** `InvestigateState`: к `LastKnownPosition`, конец по прибытии/`InvestigateDuration`; проверено пользователем.
- [x] **DINO-014** `CreatureHome` — единственное хранилище домашней точки, `SetHome` с привязкой к NavMesh.
- [x] **DINO-015** `SearchState`: случайные точки в `SearchRadius` вокруг последней позиции, паузы, лимит `SearchDuration`.
- [x] **DINO-035** `CreatureConfig.TurnSpeed` ограничивает угловую скорость поворота (`CreatureNavigator.TickRotation`, `<= 0` — поворот агентом).

## Этап C. Свободное патрулирование

- [x] **DINO-016** `SearchState.Finish`: `SetHome(Navigator.Position)`, `Memory.Clear()`, переход в `Patrol`.
- [x] **DINO-017** `CreaturePatrolPointSelector` + параметры `Patrol*` в `CreatureConfig`.
- [x] **DINO-018** `PatrolState`: ходьба по точкам вокруг дома, паузы по `PatrolWaitChance`, повтор выбора при отсутствии точки.

## Этап D. Слух

- [x] **DINO-019** `NoiseEvent` + `NoiseBroadcaster` (позиция, громкость 0..1, источник).
- [x] **DINO-020** `CreatureHearing`: дальность `HearingRange * Loudness`, игнорирует собственный шум.
- [x] **DINO-021** `ApplyNoise`: шум → приблизительная точка в памяти → `Investigate`; целью не становится.
- [x] **DINO-022** `PlayerNoiseEmitter`: шаги бега и приземление игрока.
- [x] **DINO-023** Приоритеты зрения и слуха (правила — `project-context.md`): зрение > шум, `Chase`/`Return` не прерываются, `Search` не сбивается шумом в своей области.

## Этап E. Ближняя атака и смерть игрока

- [x] **DINO-024** Изучены смерть/респавн игрока: системы здоровья и респавна в проекте нет. Контракт `IKillable.TryKill(Vector3 source)` (`false` — неуязвим/мёртв, `true` — попадание принято) описан в `project-context.md`; точка интеграции — `PlayerController` (реализует интерфейс, флаг `IsDead`, ранний выход в `Update`). Код не менялся. Поведение после смерти — `manual-queue.md`.
- [x] **DINO-025** Найти кувырок игрока и добавить i-frames (`IsInvulnerable` по таймеру, временные значения).
- [x] **DINO-026** `CreatureAttack`: дистанция, сектор, `AttackCooldown` (~1 с от конца замаха), длительность замаха, `HitTime`; попадание проверяется один раз за замах. Урон не наносить.
- [x] **DINO-027** Состояние `Attack`: старт по условиям + cooldown, после замаха — `Chase`; начатый замах не отменяется; доворот ограничен.
- [x] **DINO-028** В `HitTime` вызвать `TryKill`; `false` — промах без повтора в этом замахе. После смерти: сброс памяти, не атаковать мёртвую цель, `Patrol`. Без второй системы здоровья и урона каждый кадр.
- [x] **DINO-029** Триггер/событие анимации атаки: триггер `Attack` на старт замаха (`CreatureAnimator`), Animation Event `OnAttackHit` (`CreatureAttackAnimationEvent` → `CreatureAttack.NotifyAnimationHit`); ручная настройка клипа — `manual-queue.md`.

## Этап F. Сборка существа в Unity и доводка первой версии

- [ ] **DINO-030** Список обязательных компонентов и ссылок для префаба существа (префаб кодом не редактировать).
- [ ] **DINO-031** Статический разбор граничных случаев: пустые ссылки, невалидный агент, нет пути, уничтоженная/мёртвая цель, недоступные точки, дом вне NavMesh. Сборку/тесты не запускать.
- [ ] **DINO-032** Ручная процедура настройки в Editor: NavMesh (в радиусах патруля/поиска), слои, коллайдеры, Animator, префаб.
- [ ] **DINO-033** Обработать замечания пользователя после ручной настройки, исправлять только подтверждённое.
- [ ] **DINO-034** Зафиксировать критерии готовности: патруль вокруг дома, Investigate → Search → смена дома, смертельное попадание, уклонение кувырком, атака с интервалом ~1 с.
