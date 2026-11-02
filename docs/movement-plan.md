# План системы движения (3D, третье лицо)

Движение полностью кодом через `CharacterController`, новый Input System. Root motion и Animator не используются.

## Архитектура

### Классы

- `Player/PlayerController.cs` — точка входа. Кэш ссылок в `Awake`, создание FSM, порядок кадра `ReadInput → FSM.Tick → Motor.Apply`, единственный `Update`.
- `Player/PlayerInputReader.cs` — обёртка Input System: `Move`, `Look`, `SprintHeld`, `JumpPressed`, `RollPressed`. Без логики движения.
- `Player/CharacterMotor.cs` — исполнение движения: `CharacterController.Move`, гравитация, поворот, ground-check, coyote time. Наружу `IsGrounded`, `CanJump`, `VerticalVelocity`, `HorizontalSpeed`. Не знает о FSM.
- `Player/PlayerStamina.cs` — ресурс выносливости: текущее/максимум, расход спринта, задержка и скорость регенерации, стоимость ролла, событие изменения. Не знает о FSM и UI.
- `Player/PlayerAnimator.cs` — только публикует параметры в `Animator` (`Speed`, `Grounded`, `VerticalSpeed`, `Sprinting`, `Roll`); логики движения не содержит.
- `Player/PlayerCamera.cs` — орбитальная камера от третьего лица: `Look` → yaw/pitch с clamp, публикует `Yaw`; по горизонтали позиция жёстко на орбите, по вертикали pivot мягко догоняет цель; визуал в `LateUpdate`.
- `Player/PlayerStateMachine.cs` — текущее состояние, `ChangeState`, прокидывание `Tick(dt)`.
- `Player/States/PlayerState.cs` — базовый класс состояния (plain class): `Enter/Exit/Tick`, контекст (reader, motor, config, camera, cooldown).
- `Player/States/IdleState.cs`, `WalkState.cs`, `SprintState.cs`, `JumpState.cs`, `FallState.cs`, `RollState.cs` — по классу на файл.
- `Data/PlayerMovementConfig.cs` — ScriptableObject с тюнингом движения.
- `Data/PlayerStaminaConfig.cs` — ScriptableObject с тюнингом стамины (отдельно от движения).

### Связи

`PlayerController` создаёт FSM, стамину и состояния, передаёт им `PlayerInputReader`, `CharacterMotor`, `PlayerMovementConfig`, `PlayerStaminaConfig`, `PlayerCamera` (yaw). Состояния решают «что делать», мотор исполняет, `PlayerStamina` считает ресурс. Ввод не знает о движении, мотор — о FSM. Порядок кадра: `Stamina.Tick → Motor.Tick → FSM.Tick → Animator.Update`. Кулдаун ролла — таймер в контексте контроллера, состояния спрашивают `CanRoll`.

### FSM

```
Idle ⇄ Walk ⇄ Sprint          (|Move| и SprintHeld; Sprint только при вводе)
grounded + JumpPressed → Jump
Jump → Fall                   (VerticalVelocity ≤ 0)
Fall → Idle/Walk/Sprint       (IsGrounded)
Idle/Walk/Sprint + RollPressed && CanRoll → Roll
Roll → Idle/Walk/Sprint       (конец rollDuration; в воздухе запрещён)
```

- `Jump` — вертикальная скорость из `jumpHeight`, горизонталь с `airControl`.
- `Roll` — только с земли; направление фиксируется в `Enter` (camera-relative), персонаж мгновенно доворачивается, ввод во время ролла траекторию не меняет; скорость по `rollSpeedCurve`; кулдаун с начала ролла; `IsInvulnerable` в окне i-frames (хук для будущего урона). Без ввода — ролл вперёд (бэкстеп не входит).
- Coyote time реализуется в `CharacterMotor`; `CanJump` = на земле или coyote активен, сбрасывается при прыжке и приземлении.
- `Sprint` расходует стамину; при нуле — принудительный `Walk` и блокировка спринта, пока стамина не восстановится до порога.
- `Roll` требует `CanRoll && Stamina.CanSpendRoll`; стоимость списывается в `Enter`, при нехватке стамины ролл не начинается.
- Стамина регенерирует после `regenDelay` с момента последнего расхода; тикается контроллером до FSM.
- Jump buffer — опциональный этап 6 (таймер в контексте контроллера, потребляется grounded-состояниями).

### Параметры

**ScriptableObject `PlayerMovementConfig`:** walkSpeed, sprintSpeed, accel/decel, rotationSpeed, airControl, jumpHeight, gravity, fallGravityMultiplier, lowJumpGravityMultiplier, maxFallSpeed, coyoteTime, rollSpeedCurve, rollDuration, rollCooldown, iFramesStart/End, groundCheckRadius/offset/layerMask.

**ScriptableObject `PlayerStaminaConfig`:** maxStamina, sprintDrainPerSecond, regenPerSecond, regenDelay, sprintMinStamina, rollCost, exhaustedRecoveryThreshold.

**`[SerializeField]`:** CharacterController, Transform камеры, 5 × InputActionReference, ссылка на `PlayerMovementConfig`, ссылка на `PlayerStaminaConfig`, `Animator`, слой земли.

## Этапы

Каждый этап компилируется и проверяется в редакторе отдельно.

### [x] Этап 1. Ввод + камера

- **Файлы:** `Player/PlayerInputReader.cs`, `Player/PlayerCamera.cs`, `Player/PlayerController.cs` (временный: ссылки + лог ввода).
- **Критерий готовности:** курсор захвачен; `Look` вращает камеру (yaw/pitch с clamp); `Move` читается и виден в консоли; камера публикует `Yaw`.
- **Ручные шаги в Unity:**
  - Создать `Assets/Input/Player.inputactions`, Map `Player`: Move (Vector2), Look (Vector2), Sprint/Jump/Roll (Button).
  - Добавить `PlayerInputReader` на игрока, назначить 5 `InputActionReference`.
  - Добавить `PlayerCamera`, назначить target, высоту/дистанцию, clamp pitch.
  - Включить lock cursor.

### [x] Этап 2. CharacterMotor: гравитация, земля, coyote time

- **Файлы:** `Player/CharacterMotor.cs`, правка `Player/PlayerController.cs` (временное прямое движение без FSM), `Data/PlayerMovementConfig.cs` (базовые поля).
- **Критерий готовности:** персонаж ходит через `CharacterController.Move` относительно yaw камеры, поворачивается по направлению; падает с гравитацией, прилипает к земле; ground-check и coyote-таймер работают; состояния мотора видны в отладке.
- **Ручные шаги в Unity:**
  - Добавить `CharacterController` на игрока (высота/радиус/центр).
  - Создать слой `Ground`, назначить ground layer mask и groundCheck offset/radius.
  - Создать ассет `PlayerMovementConfig`, назначить, выставить скорость/гравитацию/coyoteTime.

### [x] Этап 3. FSM + Idle/Walk/Sprint

- **Файлы:** `Player/PlayerStateMachine.cs`, `Player/States/PlayerState.cs`, `IdleState.cs`, `WalkState.cs`, `SprintState.cs`; `PlayerController.cs` переводится на FSM (прямое движение удаляется).
- **Критерий готовности:** `Idle ⇄ Walk ⇄ Sprint` по `Move` и `SprintHeld`, спринт только при вводе; ускорение/замедление; поворот к направлению; текущее состояние видно в лог/гизмо.
- **Ручные шаги в Unity:** только тюнинг `walkSpeed`, `sprintSpeed`, `accel/decel` в SO.

### [x] Этап 4. Jump + Fall

- **Файлы:** `Player/States/JumpState.cs`, `Player/States/FallState.cs`, API мотора для вертикальной скорости.
- **Критерий готовности:** прыжок из grounded-состояний; `Jump → Fall` при `VerticalVelocity ≤ 0`; `Fall → Idle/Walk/Sprint` при приземлении; coyote-прыжок после схода с края; двойной прыжок невозможен; из воздуха нельзя.
- **Ручные шаги в Unity:** настроить `jumpHeight`, `gravity`, `fallGravityMultiplier`, `lowJumpGravityMultiplier`, `maxFallSpeed`, `airControl` в SO.

### [x] Этап 5. Roll (кувырок)

- **Файлы:** `Player/States/RollState.cs`, таймер кулдауна в контексте контроллера, поля ролла в `PlayerMovementConfig`.
- **Критерий готовности:** ролл из `Idle/Walk/Sprint` при `RollPressed` и `CanRoll`; направление camera-relative и фиксируется на входе; скорость по кривой; кулдаун; запрет в воздухе; `IsInvulnerable` в окне i-frames; после `rollDuration` — в Idle/Walk/Sprint.
- **Ручные шаги в Unity:** назначить `Roll` action; настроить `rollDuration`, `rollCooldown`, `rollSpeedCurve`, `iFramesStart/End` в SO.

### [x] Этап 6 (опционально). Jump buffer

- **Файлы:** `Player/PlayerController.cs` (таймер буфера в контексте), `IdleState/WalkState/SprintState` (потребление), поле в `PlayerMovementConfig`.
- **Критерий готовности:** нажатие за `jumpBufferTime` до приземления выполняется сразу после касания земли; буфер не копится и сбрасывается при использовании.
- **Ручные шаги в Unity:** выставить `jumpBufferTime` в SO (0.1–0.15 с).

### [x] Этап 7. Стамина (спринт + ролл)

- **Файлы:** `Player/PlayerStamina.cs`, `Data/PlayerStaminaConfig.cs`; правки `PlayerContext.cs` (ссылка на стамину), `PlayerController.cs` (тик и передача в контекст), `SprintState.cs` (расход и блокировка), `RollState.cs` (списание стоимости).
- **Критерий готовности:** спринт расходует стамину; при нуле — переход в `Walk` и запрет спринта до восстановления до порога; ролл не начинается при нехватке стамины; регенерация стартует через `regenDelay`; событие изменения стамины пригодно для HUD; расход и регенерация не зависят от FPS.
- **Ручные шаги в Unity:** создать ассет `PlayerStaminaConfig`, назначить в `PlayerController`, выставить максимум, расход, регенерацию, задержку, стоимость ролла и пороги.

### [~] Этап 8. Анимации

- **Осталось:** после приземления анимация `Fall` прерывается не сразу, `Grounded` переходит `False → True` с задержкой. Нужно убрать кадровую задержку ground-check (состояние земли считается в `CharacterMotor.UpdateGroundState` до `CharacterController.Move`, поэтому касание земли этого кадра видно только на следующем) и проверить, что `Has Exit Time` на `Fall → Locomotion` выключен.
- **Файлы:** `Player/PlayerAnimator.cs`; правка `PlayerController.cs` (передача состояния после `FSM.Tick`).
- **Критерий готовности:** анимация соответствует FSM: Idle/Walk/Run по `Speed`, Jump/Fall по `Grounded` и `VerticalSpeed`, Roll по триггеру; root motion выключен, движение остаётся за кодом; нет «залипаний» и рассинхрона с физикой.
- **Ручные шаги в Unity:**
  - Создать Animator Controller (вручную, не агентом) с параметрами `Speed` (float), `Grounded` (bool), `VerticalSpeed` (float), `Sprinting` (bool), `Roll` (trigger).
  - Собрать blend tree локомоции Idle→Walk→Run по `Speed`, состояния Jump/Fall и состояние Roll с возвратом по окончании клипа.
  - Выключить `Apply Root Motion`, `Update Mode` = Normal.
  - Добавить `PlayerAnimator` на игрока и назначить `Animator`.

## Решения и заметки

- Этапы 1–7 реализованы и проверены в редакторе, этап 8 проверен частично (см. пункт «Осталось»). Этап 4 после первой проверки доработан: добавлена асимметричная гравитация и переменная высота прыжка.
- Камера: мышь, свободная орбита от третьего лица без авто-центрирования (Souls-like).
- Чувствительность камеры — в градусах на пиксель: 0.15 по горизонтали, 0.10 по вертикали.
- Сглаживание поворота по умолчанию выключено (`_rotationSmoothing = 0`) ради «сырого» отклика мыши.
- Позиция камеры жёстко на орбите, поворот через `LookRotation` на pivot. Мировой `Lerp` позиции убран: он вызывал уход персонажа из центра кадра при высокой чувствительности.
- Камера по вертикали мягко догоняет pivot (`verticalFollowTime`, `verticalFollowMaxOffset`), по горизонтали остаётся жёсткой — прыжок читается по ускорению, персонаж не теряется из кадра при падении.
- Coyote time живёт в `CharacterMotor`. Grounded-состояния при отрыве сразу уходят в `Fall`, прыжок из `Fall` разрешён только пока coyote активен — так двойной прыжок невозможен. Ролл временно недоступен до этапа 5.
- В воздухе `airControl` масштабирует и ускорение, и замедление (горизонтальный импульс затухает дольше); без ввода направление берётся из forward персонажа, чтобы не останавливаться мгновенно.
- Прыжок: импульс `sqrt(2·g·jumpHeight)` считается по базовой гравитации, поэтому высота не зависит от множителей. При падении гравитация × `fallGravityMultiplier`, при отпущенной кнопке на подъёме × `lowJumpGravityMultiplier` — короткое нажатие даёт низкий прыжок, падение ускоряется, дуга перестаёт быть симметричной.
- Ролл без ввода — вперёд; бэкстеп (как в Souls) вне рамок задачи.
- Root motion и Animator не используются, движение полностью кодом.
- Анимации — только визуализация FSM через `PlayerAnimator`, параметры не влияют на физику.
- Jump buffer — опциональный этап 6.
- Этап 5, отклонения от плана:
  - В `PlayerMovementConfig` добавлено поле `_rollSpeed` (базовая скорость ролла, по умолчанию 8). В списке параметров его не было, а `rollSpeedCurve` трактуется как множитель (значение × `RollSpeed`), иначе значения кривой 0.2–1 нельзя использовать как м/с.
  - Кулдаун живёт в `PlayerContext`: `RollCooldownRemaining`, `CanRoll`, `Tick(dt)`, `StartRollCooldown()`. Тикается в `PlayerController.Update` первым, до `Motor.Tick` и FSM. Кулдаун стартует в `RollState.Enter` от `rollCooldown` и включает время самого ролла.
  - `PlayerContext.IsInvulnerable` — публичный сеттер-хук для будущего урона; его выставляет только `RollState` (по `_elapsed` в окне `IFramesStart..IFramesEnd`), сбрасывается в `Exit`.
  - В `CharacterMotor` добавлены `FaceDirection(dir)` (мгновенный доворот по плоскому направлению) и `MoveInstant(dir, speed, dt)` (сразу ставит `_currentSpeed`, минуя accel/decel). Обычный `Move` с ускорением для ролла не подходит.
  - В `PlayerContext` добавлено `CameraForward`, общий приватный `YawRotation` используется и в `MoveDirection` — ролл без ввода катится по forward камеры, а не по forward персонажа.
  - `RollState` при потере земли посреди ролла прерывается в `Fall`; проверка старта ролла — `IsGrounded` (не `CanJump`), поэтому из coyote-окна ролл недоступен.
  - `TryStartRoll()` в базовом `PlayerState` вызывается в `Idle/Walk/Sprint` до `TryLeaveGround`, то есть ролл приоритетнее прыжка в один кадр.
  - Проверка стамины для ролла (`Stamina.CanSpendRoll`) отложена до этапа 7; сейчас ограничение только кулдаун.
- Этап 6, отклонения от плана:
  - `PlayerMovementConfig._jumpBufferTime` (`JumpBufferTime`, по умолчанию 0.12 с) добавлено в блок `Jump & Gravity`.
  - Буфер живёт в `PlayerContext`: `JumpBufferRemaining`, `HasJumpBuffered`, `BufferJump()`, `ConsumeJumpBuffer()`; декремент — в `PlayerContext.Tick`.
  - `PlayerController` сначала тикает контекст, затем при `JumpPressed` вызывает `BufferJump()` — таймер не теряет кадр; порядок до `Motor.Tick` и FSM сохранён.
  - `PlayerState.TryLeaveGround` срабатывает по `JumpPressed || HasJumpBuffered`; сброс буфера — в `JumpState.Enter` после успешного `Motor.TryJump` (а не в состоянии-потребителе), поэтому буфер гасится ровно в момент прыжка и не «съедается» при неудаче.
  - Каждое новое нажатие перезаписывает таймер (не суммируется). Спринт/ролл приоритетнее прыжка в один кадр: ролл проверяется раньше, нажатие прыжка во время ролла сработает сразу после его окончания.
  - На приземлении прыжок выполняется на первом `Tick` grounded-состояния (задержка ~1 кадр), поскольку буфер потребляют именно grounded-состояния, а `FallState` только переключает состояние.
- Этап 7, отклонения от плана:
  - `PlayerStamina` — обычный класс (не MonoBehaviour), создаётся внутри `PlayerContext` из `PlayerStaminaConfig`. API: `Current/Max/Normalized`, `IsExhausted`, `CanStartSprint`, `CanContinueSprint`, `CanSpendRoll`, `Tick(dt)`, `DrainSprint(dt)`, `TrySpendRoll()`; событие `Changed(current, max)` для будущего HUD (поднимается только при реальном изменении значения).
  - Конструктор `PlayerContext` получил четвёртым параметром `PlayerStaminaConfig`; `PlayerContext.Stamina` — единая точка доступа. Это меняет сигнатуру, единственный вызов — в `PlayerController`.
  - `PlayerController` получил `[SerializeField] private PlayerStaminaConfig _staminaConfig`; если ассет не назначен — предупреждение в консоль, спринт и ролл выключаются (а не падение). Порядок кадра: `Stamina.Tick → Context.Tick(кулдаун/буфер) → Motor.Tick → FSM.Tick`.
  - Логика блокировки спринта вынесена в новые члены: `PlayerStamina.CanStartSprint` (не exhausted и `current >= sprintMinStamina`) и `CanContinueSprint` (не exhausted и `current > 0`). Поэтому спринт не обрывается при падении ниже `sprintMinStamina`, а только на нуле; повторный старт требует порога.
  - `_exhausted` ставится при любом обнулении стамины (спринт или ролл), снимается только когда регенерация доходит до `exhaustedRecoveryThreshold`. Регенерация начинается после `regenDelay` с момента последней траты; таймер задержки перезапускается и при спринте, и при ролле.
  - Вместо правки только `SprintState` добавлен общий `PlayerState.ResolveGroundedState()`, который учитывает стамину; его используют `IdleState`, `WalkState`, `FallState` и `RollState` (приватные дубли `ResolveGroundedState` в `FallState`/`RollState` удалены). `WalkState` проверяет `CanStartSprint` перед переходом в спринт.
  - `TryStartRoll` теперь требует ещё и `Stamina.CanSpendRoll`; `RollState.Enter` списывает `rollCost` через `TrySpendRoll()`.
  - Отладочный лог дополнен `St=current/max` и `Exh`, состояние исчерпания тоже включает логирование.
- Этап 8, отклонения от плана:
  - `PlayerAnimator.Tick(PlayerContext, PlayerStateMachine)` — единственный публичный метод; вызывается `PlayerController` сразу после `FSM.Tick`. Ссылка на компонент берётся из `[SerializeField] _animator` в контроллере или через `GetComponent` в `Awake`; при отсутствии аниматора вызов пропускается.
  - `Speed` публикуется как фактическая горизонтальная скорость `CharacterMotor.HorizontalSpeed` (velocity `CharacterController`), а не как целевая скорость состояния — так blend tree совпадает с физикой при ускорении/замедлении.
  - `Sprinting` = текущее состояние `SprintState` (ссылка на состояние), `Grounded`/`VerticalSpeed` — из мотора.
  - `Roll` — триггер по фронту входа в `RollState` (флаг `_wasRolling`), поэтому срабатывает один раз за ролл. Обратного параметра нет: возврат из клипа ролла — по `Has Exit Time` в Animator.
  - `PlayerAnimator.Awake` сам ищет `Animator` в дочерних объектах и принудительно ставит `applyRootMotion = false`; хэши параметров закэшированы через `Animator.StringToHash`.
  - В `PlayerController` добавлено поле `[SerializeField] private PlayerAnimator _animator`.
- Пост-фикс после проверки этапа 8:
  - `CharacterMotor` хранит последнее горизонтальное направление `_moveDirection`; `Move` при нулевом `direction` (остановка) больше не обнуляет смещение, поэтому `Deceleration` реально тормозит, а `HorizontalSpeed`/параметр `Speed` убывают плавно. Раньше `Stop` передавал `Vector3.zero`, и скорость падала до нуля за один кадр — Blend Tree мгновенно уходил в Idle.
  - В Animator `Has Exit Time` должен быть выключен у всех переходов, кроме `Roll → Locomotion`; иначе клип Jump/Fall доигрывается до конца и анимация не реагирует на приземление. Это настройка ассета, код её не контролирует.
- Этап 8, проверка в редакторе:
  - Работает: анимации Idle/Walk/Run, Jump/Fall, Roll.
  - Известный баг: после приземления анимация `Fall` прерывается не сразу.
  - Известный баг: параметр `Grounded` переключается `False → True` с задержкой.
  - Причина задержки: `CharacterMotor.UpdateGroundState` вызывается в `Motor.Tick` до `FSM.Tick`/`CharacterController.Move`, поэтому касание земли, произошедшее в текущем кадре, читается только на следующем.
  - `_controller.isGrounded` обновляется только после `Move`, а `CheckGround` проверяет сферу с `GroundCheckOffset`/`GroundCheckRadius` — оба фактора добавляют кадр(ы) к обнаружению земли.
  - `Has Exit Time` на `Fall → Locomotion` — настройка Animator-ассета, код её не контролирует; при включённом exit time `Fall` доигрывается независимо от `Grounded`.
  - Замечание пользователя: задержка `Grounded` ощущается как «залипание» анимации падения после касания земли.
- Пост-фикс приземления (требует проверки в редакторе):
  - `CharacterMotor.Move` пересчитывает землю сразу после `CharacterController.Move` и учитывает `CollisionFlags.Below` из возврата `Move`, поэтому касание земли видно в том же кадре.
  - `PlayerAnimator` публикует `VerticalSpeed = 0`, когда `IsGrounded`, чтобы переход `Locomotion → Fall` не срабатывал из-за внутренних `-2` (прилипание к земле).
  - `FallState.Tick` после `MoveAir` повторно проверяет `IsGrounded` и переключается в Idle/Walk/Sprint в кадре приземления.