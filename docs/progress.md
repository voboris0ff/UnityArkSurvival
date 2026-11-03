# Текущий прогресс

## Общий статус
- Unity 6 / URP.
- Передвижение персонажа реализовано.
- Текущий приоритет разработки: первое существо — наземный динозавр-хищник.
- Следом запланированы интерфейс персонажа и карта.

## Реализовано
### Игрок
- Движение через CharacterController и новый Input System.
- FSM: Idle, Walk, Sprint, Jump, Fall, Roll.
- Jump buffer и coyote time.
- Выносливость.
- Присед как отдельный слой стойки.
- Камера от третьего лица с коллизиями и сглаживанием.
- Публикация параметров в Animator.

### Существо
- В `Assets/Scripts/AI/` пока есть только `Assets/Scripts/AI/Creatures/CreatureNavigator.cs` (DINO-003). MonoBehaviour существа ещё нет.
- Создан класс данных `Assets/Scripts/Data/CreatureConfig.cs` (DINO-002). ScriptableObject-ассетов существ нет.
- Пакет `com.unity.ai.navigation` 2.0.15 установлен (проект использует режим «AI Navigation» — сборки `Unity.AI.Navigation`).
- В `SampleScene` секция `NavMeshSettings` содержит `m_NavMeshData: {fileID: 0}` — навигационная сетка ещё не запечена.
- План реализации находится в `docs/roadmap.md`, задачи DINO-001–DINO-030.

## Результаты DINO-001 (аудит и контракт CreatureController)

### Найденная структура и зависимости
- `Assets/Scripts/Data/` — плоская папка ScriptableObject-конфигов, namespace `ArkSurvival.Data`. Образец стиля: `[CreateAssetMenu(fileName = "...", menuName = "ArkSurvival/...")] public sealed class ...Config : ScriptableObject` с `[Header]`, `[SerializeField] private`, публичными get-only свойствами.
- `Assets/Scripts/Player/` — namespace `ArkSurvival.Player`. Паттерны, которые можно переиспользовать:
  - `PlayerController` — единственный MonoBehaviour-композитор: `[SerializeField]` ссылки, `Awake` с фолбэком `GetComponent`, `Start` с проверкой ссылок и `Debug.LogWarning`, создание контекста и FSM, `Update` как единственный тик.
  - `PlayerContext` — не-MonoBehaviour контейнер зависимостей (plain C#, передаётся в состояния).
  - `PlayerStateMachine` — простая FSM: поля-состояния, `Current/CurrentName`, `Start()`, `Tick(dt)`, `ChangeState(next)` с защитой от null и повторного входа.
  - `PlayerState` (abstract, namespace `ArkSurvival.Player.States`) — `Enter()`, `Exit()`, abstract `Tick(float)`, общие хелперы. Состояния не MonoBehaviour.
- `PlayerInputReader` (новый Input System), `CharacterMotor` (CharacterController) — относятся только к игроку; существо их не использует.
- Игрок в сцене: объект `Player`, **layer 6**, `CharacterController` (height 1.73, center.y 0.945, radius 0.2), дочерний `Visual`.
- Игрок ищется существом по компоненту; тега и системы здоровья у игрока нет, `Health`/`Damage`/`Attack` в `Assets/Scripts` отсутствуют.

### Ключевые выводы, влияющие на реализацию
- Система здоровья/урона отсутствует → подтверждается необходимость DINO-021 (согласование контракта урона до DINO-024).
- Animator существа и анимации отсутствуют, имена параметров неизвестны → DINO-007 и DINO-025 без ручного подтверждения пользователя выполнить нельзя.
- NavMesh не запечён, слои для препятствий/цели не выделены → требуется ручная настройка (manual-queue, DINO-028).
- Игрок на layer 6 (неизвестное имя слоя) → referencing слоя «Player» через `LayerMask`/`GetComponent` лучше, чем по имени слоя; настройка LayerMask — ручная.

### Утверждённый минимальный контракт
Схема потока: `CreatureController` (MonoBehaviour, композиция и тик) → `CreatureConfig` (данные) → `CreatureNavigator` (исполнение навигации) ; `CreatureStateMachine` + состояния (`IdleState`, `ReturnState`, `InvestigateState`, `ChaseState`, `SearchState`, `AttackState`) приним��ют решения. Восприятие (`CreaturePerception`), память (`CreatureTargetMemory`) и атака (`CreatureAttack`) — отдельные компоненты, ошибки не принимают.

Границы ответственности:
- `CreatureController` — единственный `Update`-тик существа: валидация ссылок в `Awake`/`Start`, создание `CreatureNavigator`/состояний, порядок обновления, `[SerializeField] Transform _homePoint` (если не задан — стартовая позиция). Никакой политики поведения.
- `CreatureNavigator` — только `NavMeshAgent`: `SetDestination`, `Stop`, скорость, `HasPath`, безопасные проверки (`isOnNavMesh`, `enabled`, `isActiveAndEnabled`). Не решает, куда идти.
- `CreatureStateMachine`/состояния — переводы состояний; состояния не трогают `NavMeshAgent` напрямую, а вызывают navigator/attack/memory.
- `CreaturePerception` — выдаёт факт «вижу»/«слышу» и позицию, без принятия решений.
- `CreatureTargetMemory` — хранит цель, последнюю известную позицию, время обнаружения, истечение памяти.
- `CreatureAttack` — проверка дистанции/сектора/cooldown и одна атака; выбор цели не в нём.

### План файлов (порождается задачами DINO-002..DINO-007)
```
Assets/Scripts/Data/CreatureConfig.cs                      (DINO-002, namespace ArkSurvival.Data)
Assets/Scripts/AI/Creatures/CreatureController.cs          (DINO-004, namespace ArkSurvival.AI.Creatures)
Assets/Scripts/AI/Creatures/CreatureNavigator.cs           (DINO-003)
Assets/Scripts/AI/Creatures/CreatureStateMachine.cs        (DINO-005)
Assets/Scripts/AI/Creatures/CreatureStates/CreatureState.cs + IdleState.cs (DINO-005)
Assets/Scripts/AI/Creatures/CreatureStates/ReturnState.cs (DINO-006, условно)
Assets/Scripts/AI/Creatures/CreaturePerception.cs          (DINO-008–DINO-011)
Assets/Scripts/AI/Creatures/CreatureTargetMemory.cs        (DINO-008)
Assets/Scripts/AI/Creatures/CreatureAttack.cs              (DINO-022)
Assets/Scripts/AI/Creatures/CreatureAnimator.cs            (DINO-007/025, только после подтверждения параметров)
```
Не создавать эти файлы сейчас — DINO-001 не меняет код. Найденную директорию `Assets/Scripts/AI/Creatures/` создаёт Unity при появлении первого файла (meta делает Unity).

## Результаты DINO-002 (CreatureConfig)

- Создан `Assets/Scripts/Data/CreatureConfig.cs`, namespace `ArkSurvival.Data`, `sealed`, меню `ArkSurvival/Creature Config`.
- Стиль совпадает с остальными конфигами: `[Header]`, `[SerializeField] private`, публичные get-only свойства.
- Параметры по группам:
  - `Navigation`: `MoveSpeed`, `ChaseSpeed`, `TurnSpeed`, `ArrivalDistance`, `DestinationUpdateInterval`.
  - `Perception`: `PerceptionInterval`, `SightDistance`, `SightAngle` (полный угол конуса, градусы), `EyeHeight`, `ObstacleLayerMask`, `HearingRange`.
  - `Memory`: `MemoryDuration` (время памяти о цели), `SearchDuration` (лимит состояния Search).
  - `Attack`: `AttackRange`, `AttackAngle` (полный сектор, градусы), `AttackCooldown`, `AttackWindup`, `AttackDamage`.
- Значения — стартовые, подобраны как нейтральные для тестового существа; игровые числа не утверждены.
- `AttackDamage` задан только как параметр `CreatureConfig`. Это **не** система здоровья и не контракт урона: способ применения урона к игроку по-прежнему не согласован (DINO-021, manual-queue).
- `.asset` не создавался. Создание ассета и назначение его в `CreatureController` — ручной шаг (DINO-004/DINO-026).
- Потребителей у конфига пока нет; первые появятся в DINO-003/DINO-004.

## Результаты DINO-003 (CreatureNavigator)

- Создан `Assets/Scripts/AI/Creatures/CreatureNavigator.cs`, namespace `ArkSurvival.AI.Creatures`, `sealed`, plain C# (не MonoBehaviour).
- `NavMeshAgent` передаётся в конструкторе и кэшируется; компонент не ищется в рантайме (это делает `CreatureController` в DINO-004).
- Публичный API:
  - `IsValid` — агент назначен; `IsReady` — агент включён, активен и стоит на NavMesh (`isOnNavMesh`);
  - `HasPath` — есть актуальный путь (`!pathPending && !pathInvalid`), `IsMoving`, `HasArrived` (горизонтальная дистанция до `destination`);
  - `Position`, `RemainingDistance`;
  - `Initialize(arrivalDistance, defaultSpeed)` — `autoBraking`/`autoRepath` включены, `stoppingDistance` не меньше дистанции прибытия;
  - `TrySetDestination(position, allowPartialPath = false)` — снимает стоп, передаёт точку через `NavMesh.SamplePosition` с фильтром `agentTypeID`/`areaMask`; при недостижимой/частичной поверхности возвращает `false` и сбрасывает путь;
  - `SetSpeed(speed)`, `ResetToSpeed()`, `Stop()` (стоп + `ResetPath`), `SetUpdateRotation(bool)`.
- `TrySetDestination` начинается с `ResetPath()`, поэтому повторная установка цели не вызывает побочного отклонения частичного пути от предыдущего назначения.
- Без выданных целей класс ничего не делает; политика (когда и куда идти) останется в состояниях.
- `Stop()` устанавливает `isStopped = true`; вернуть движение можно только через `TrySetDestination`.
- `Initialize` вызывается из `CreatureController` при `Start`; в редакторе поля агента (`speed`, `stoppingDistance`) будут перезаписаны кодом.
- Проверок каждый кадр нет; физических проверок внутри нет.

## Текущая задача
Следующая по дорожной карте — DINO-004 (класс `CreatureController`).

## Принятые ограничения
- Агент не редактирует Unity Assets/сцены/префабы.
- Агент не запускает сборки и тесты.
- Настройка NavMesh, префаба, Animator, слоёв и коллайдеров выполняется пользователем в Unity Editor.
- Каждая задача roadmap выполняется в отдельной сессии.
- Неизвестные игровые решения не придумывать: фиксировать в `manual-queue.md`.

## История
Подробные логи автоматических сессий сохраняются в `docs/session-logs/`.
