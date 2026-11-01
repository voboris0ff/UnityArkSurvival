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

### [ ] Этап 5. Roll (кувырок)

- **Файлы:** `Player/States/RollState.cs`, таймер кулдауна в контексте контроллера, поля ролла в `PlayerMovementConfig`.
- **Критерий готовности:** ролл из `Idle/Walk/Sprint` при `RollPressed` и `CanRoll`; направление camera-relative и фиксируется на входе; скорость по кривой; кулдаун; запрет в воздухе; `IsInvulnerable` в окне i-frames; после `rollDuration` — в Idle/Walk/Sprint.
- **Ручные шаги в Unity:** назначить `Roll` action; настроить `rollDuration`, `rollCooldown`, `rollSpeedCurve`, `iFramesStart/End` в SO.

### [ ] Этап 6 (опционально). Jump buffer

- **Файлы:** `Player/PlayerController.cs` (таймер буфера в контексте), `IdleState/WalkState/SprintState` (потребление), поле в `PlayerMovementConfig`.
- **Критерий готовности:** нажатие за `jumpBufferTime` до приземления выполняется сразу после касания земли; буфер не копится и сбрасывается при использовании.
- **Ручные шаги в Unity:** выставить `jumpBufferTime` в SO (0.1–0.15 с).

### [ ] Этап 7. Стамина (спринт + ролл)

- **Файлы:** `Player/PlayerStamina.cs`, `Data/PlayerStaminaConfig.cs`; правки `PlayerContext.cs` (ссылка на стамину), `PlayerController.cs` (тик и передача в контекст), `SprintState.cs` (расход и блокировка), `RollState.cs` (списание стоимости).
- **Критерий готовности:** спринт расходует стамину; при нуле — переход в `Walk` и запрет спринта до восстановления до порога; ролл не начинается при нехватке стамины; регенерация стартует через `regenDelay`; событие изменения стамины пригодно для HUD; расход и регенерация не зависят от FPS.
- **Ручные шаги в Unity:** создать ассет `PlayerStaminaConfig`, назначить в `PlayerController`, выставить максимум, расход, регенерацию, задержку, стоимость ролла и пороги.

### [ ] Этап 8. Анимации

- **Файлы:** `Player/PlayerAnimator.cs`; правка `PlayerController.cs` (передача состояния после `FSM.Tick`).
- **Критерий готовности:** анимация соответствует FSM: Idle/Walk/Run по `Speed`, Jump/Fall по `Grounded` и `VerticalSpeed`, Roll по триггеру; root motion выключен, движение остаётся за кодом; нет «залипаний» и рассинхрона с физикой.
- **Ручные шаги в Unity:**
  - Создать Animator Controller (вручную, не агентом) с параметрами `Speed` (float), `Grounded` (bool), `VerticalSpeed` (float), `Sprinting` (bool), `Roll` (trigger).
  - Собрать blend tree локомоции Idle→Walk→Run по `Speed`, состояния Jump/Fall и состояние Roll с возвратом по окончании клипа.
  - Выключить `Apply Root Motion`, `Update Mode` = Normal.
  - Добавить `PlayerAnimator` на игрока и назначить `Animator`.

## Решения и заметки

- Этапы 1–4 проверены в редакторе. Этап 4 после первой проверки доработан: добавлена асимметричная гравитация и переменная высота прыжка. Следующий — этап 5 (Roll).
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