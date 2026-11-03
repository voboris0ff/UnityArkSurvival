# План системы движения (3D, третье лицо)

Движение полностью кодом через `CharacterController`, новый Input System. Root motion и Animator не используются.

## Архитектура

### Классы

- `Player/PlayerController.cs` — точка входа. Кэш ссылок в `Awake`, создание FSM и слоя стойки, порядок кадра `Stamina.Tick → Context.Tick → Crouch.Tick → Motor.Tick → FSM.Tick → Animator.Tick`, единственный `Update`.
- `Player/PlayerInputReader.cs` — обёртка Input System: `Move`, `Look`, `SprintHeld`, `JumpPressed`, `JumpHeld`, `RollPressed`, `CrouchPressed`, `CrouchHeld`. `Look` — сырое значение плюс признак устройства (мышь/геймпад) для раздельной обработки. Без логики движения.
- `Player/CharacterMotor.cs` — исполнение движения: `CharacterController.Move`, гравитация, поворот, ground-check, coyote time. Наружу `IsGrounded`, `CanJump`, `VerticalVelocity`, `HorizontalSpeed`. Принимает раздельные accel/decel; ground-check привязан к низу капсулы. Не знает о FSM.
- `Player/PlayerCrouchController.cs` — ортогональный слой стойки: режим toggle/hold, `WantsCrouch`, `IsCrouched`, `CanStand`, `CrouchAmount`; измерение свободной высоты, плавное изменение `height`/`center` капсулы с фиксацией низа. Не FSM-состояние, не знает о камере и аниматоре.
- `Player/PlayerStamina.cs` — ресурс выносливости: текущее/максимум, расход спринта, задержка и скорость регенерации, стоимость ролла, событие изменения. Не знает о FSM и UI.
- `Player/PlayerAnimator.cs` — только публикует параметры в `Animator` (`Speed`, `Grounded`, `VerticalSpeed`, `Sprinting`, `Roll`, `Crouching`, `CrouchAmount`); логики движения не содержит.
- `Player/PlayerCamera.cs` — камера от третьего лица: раздельная обработка мыши и стика, сырые yaw/pitch с clamp, публикация сырого `Yaw` для геймплея, визуальное сглаживание/инерция только для позы, вертикальное следование по Y, плечо, динамический FOV, снижение pivot по `CrouchAmount`. Позу ставит в `LateUpdate` после движения.
- `Player/PlayerCameraCollision.cs` — plain class: SphereCast от pivot к желаемой позиции, мгновенное приближение, плавный возврат, минимальная дистанция, исключение игрока. Не знает о вводе.
- `Player/PlayerStateMachine.cs` — текущее состояние, `ChangeState`, прокидывание `Tick(dt)`.
- `Player/States/PlayerState.cs` — базовый класс состояния (plain class): `Enter/Exit/Tick`, контекст (reader, motor, config, camera, crouch, cooldown).
- `Player/States/IdleState.cs`, `WalkState.cs`, `SprintState.cs`, `JumpState.cs`, `FallState.cs`, `RollState.cs` — по классу на файл.
- `Data/PlayerMovementConfig.cs` — ScriptableObject с тюнингом движения.
- `Data/PlayerStaminaConfig.cs` — ScriptableObject с тюнингом стамины (отдельно от движения).
- `Data/PlayerCameraConfig.cs` — ScriptableObject с тюнингом камеры (отдельно от движения), включая crouch-высоту.
- `Data/PlayerCrouchConfig.cs` — ScriptableObject с тюнингом приседа (отдельно от движения): режим ввода, высота/центр капсулы, скорость перехода, проверка потолка, скорость и ускорения приседа, `stepOffset`.

### Связи

`PlayerController` создаёт FSM, стамину, слой стойки и состояния, передаёт им `PlayerInputReader`, `CharacterMotor`, `PlayerMovementConfig`, `PlayerStaminaConfig`, `PlayerCrouchConfig`, `PlayerCamera` (yaw). Состояния решают «что делать», мотор исполняет, `PlayerStamina` считает ресурс, `PlayerCrouchController` держит геометрию стойки. Ввод не знает о движении, мотор — о FSM, присед — о камере и аниматоре. Порядок кадра: `Stamina.Tick → Context.Tick → Crouch.Tick → Motor.Tick → FSM.Tick → Animator.Tick`; капсула меняется до движения и ground-check. Кулдаун ролла — таймер в контексте контроллера, состояния спрашивают `CanRoll`.

Камера: `Update` читает `Look` и обновляет сырые углы; `LateUpdate` строит позу после движения игрока. Геймплей использует только сырой `Yaw` (`PlayerContext.YawRotation`), визуальные сглаживание и инерция на него не влияют.

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
- Jump buffer — таймер в контексте контроллера, потребляется grounded-состояниями.
- Присед — не FSM-состояние, а ортогональный слой стойки: `Crouch.Tick` меняет капсулу до `Motor.Tick`, FSM продолжает работать в Idle/Walk/Jump/Fall/Roll.
- Вход в присед отменяет спринт; в приседе `ResolveGroundedState` возвращает только Idle/Walk, спринт игнорируется.
- Прыжок из приседа: если `CanStand`, присед снимается и прыжок выполняется в том же кадре; если потолок не позволяет — прыжок игнорируется, jump buffer сбрасывается.
- Перекат из приседа: если `CanStand`, встаём и перекатываемся в том же кадре; если потолок не позволяет — перекат запрещён.
- Падение из приседа: в воздухе остаёмся в приседе, вставание — после приземления и проверки потолка.
- Присед не меняет yaw/фейсинг, поэтому будущий лок-он сможет управлять поворотом независимо.

### Параметры

**ScriptableObject `PlayerMovementConfig`:** walkSpeed, sprintSpeed, accel/decel, rotationSpeed, airControl, jumpHeight, gravity, fallGravityMultiplier, lowJumpGravityMultiplier, maxFallSpeed, coyoteTime, jumpBufferTime, rollSpeedCurve, rollSpeed, rollDuration, rollCooldown, iFramesStart/End, groundCheckRadius/offset/layerMask.

**ScriptableObject `PlayerStaminaConfig`:** maxStamina, sprintDrainPerSecond, regenPerSecond, regenDelay, sprintMinStamina, rollCost, exhaustedRecoveryThreshold.

**ScriptableObject `PlayerCrouchConfig`:** crouchInputMode (Toggle/Hold), crouchHeight, crouchCenterY, heightTransitionSpeed, ceilingSkin, ceilingLayerMask, crouchSpeed, crouchAcceleration, crouchDeceleration, crouchStepOffset.

**ScriptableObject `PlayerCameraConfig`:** distance, lookHeight, crouchLookHeight, shoulderOffset, pitchMin/pitchMax (−30…55), startPitch, mouseDegreesPerPixel (h/v), stickDegreesPerSecond (h/v), stickDeadZone, sensitivityCurve, verticalFollowTime, verticalFollowMaxOffset, rotationSmoothing (инерция), collisionRadius, collisionLayerMask, minDistance, collisionReturnSpeed, baseFov, sprintFov, rollFov, fovSmoothTime.

**`[SerializeField]`:** CharacterController, Transform камеры, 6 × InputActionReference, ссылка на `PlayerMovementConfig`, ссылка на `PlayerStaminaConfig`, ссылка на `PlayerCrouchConfig`, ссылка на `PlayerCameraConfig`, `Animator`, слой земли.

## Этапы

Каждый этап компилируется и проверяется в редакторе отдельно.

### [x] Реализовано: движение, стамина, анимации, камера (этапы 1–8, К1–К6)

Реализовано и проверено: ввод, `CharacterMotor`, FSM (Idle/Walk/Sprint/Jump/Fall/Roll), jump buffer, стамина, анимации; ввод камеры с разделением мышь/стик, pitch clamp, сырой `Yaw`, вертикальное следование, плечо, коллизии, инерция и динамический FOV. Детали и отклонения — в «Решениях и заметках».

### [x] Реализовано: капсула в ролле (этап 15)

Присед реализуется как ортогональный слой стойки, а не как новые FSM-состояния. Ввод — toggle по умолчанию, hold как настройка. Радиус капсулы не меняется: только `height` и `center`, низ капсулы фиксируется относительно `transform.position`. Шум и стелс-радиус в план не входят.

#### [x] 9. Ввод и слой стойки

- Новое действие `Crouch` (клавиатура + геймпад), `CrouchPressed`/`CrouchHeld` в `PlayerInputReader`.
- `PlayerCrouchConfig` с режимом `Toggle/Hold`; toggle по умолчанию.
- `PlayerCrouchController`: `WantsCrouch`, `IsCrouched`, `CanStand`, `CrouchAmount`, тик до `CharacterMotor.Tick`.
- Капсула и камера ещё не меняются; проверка — корректные флаги и лог.

#### [x] 10. Капсула и проверка потолка

- Плавное изменение `height`/`center` с фиксацией низа капсулы, радиус без изменений; `stepOffset` ограничивается безопасной величиной.
- Измерение свободной высоты вверх (`CheckCapsule`/`CapsuleCast`, слой потолка, исключение игрока); рост капсулы не превышает свободную высоту.
- Если встать некуда — игрок остаётся в приседе; при освобождении места встаёт автоматически. Повторное нажатие приседа отменяет ожидание вставания.
- Ground-check привязан к фактическому низу капсулы и не даёт ложный `Fall` в переходном кадре.
- Проверка: низкий проход, плавный вход/выход, отсутствие проваливания и дёрганья.

#### [x] 11. Скорость и переходы между состояниями

- `crouchSpeed` и отдельные `crouchAcceleration`/`crouchDeceleration`; `CharacterMotor.Move` принимает rates, существующие вызовы сохраняют поведение.
- Вход в присед отменяет спринт; в приседе спринт игнорируется, `ResolveGroundedState` возвращает Idle/Walk.
- Прыжок: при `CanStand` — встать и прыгнуть в том же кадре; иначе прыжок игнорируется и jump buffer сбрасывается.
- Перекат: при `CanStand` — встать и перекатиться; иначе перекат запрещён. Направление, i-frames и кулдаун не меняются.
- Падение: из приседа можно уйти с края, в воздухе стойка сохраняется, вставание после приземления.
- Проверка: Idle/Walk/присед, спринт, прыжок, ролл, падение.

#### [x] 12. Камера

- `crouchLookHeight` в `PlayerCameraConfig`; целевая высота pivot интерполируется по `CrouchAmount`.
- Используется существующее вертикальное сглаживание (`FollowPivotHeight`), без второго параллельного сглаживания и без рывков.
- `PlayerCameraCollision`, радиус и маска коллизий не меняются; меняется только pivot.
- Проверка: вход/выход из приседа, потолки, стены, отсутствие джиттера.

#### [x] 13. Анимации

- `PlayerAnimator` публикует `Crouching` и `CrouchAmount`; `Speed` остаётся фактической горизонтальной скоростью, `Sprinting` в приседе всегда `false`.
- `CrouchAmount` синхронизируется с фактической высотой капсулы, а не с булевым флагом — ноги не отрываются от земли.
- Blend tree приседа и переходы вход/выход настраиваются в Animator-ассете вручную.
- Проверка: переходы без рывков, совпадение анимации и капсулы, ролл и прыжок из приседа.

#### [x] 14. Поверхности, уклоны, ступени

- `stepOffset` при низкой капсуле не позволяет застревать на ступенях и не даёт проваливаться.
- Проверка склонов, разных поверхностей и краёв: в приседе не соскальзывает и не застревает.
- Регрессия спринта, ролла, прыжка, камеры и ground-check.

#### [x] 15. Капсула в ролле и возврат стойки

- На время ролла капсула уменьшается как при приседе (`height`/`center`, низ фиксирован), через отдельный временный модификатор, без установки `IsCrouched`.
- `stepOffset` на время ролла ограничивается как при низкой капсуле; ground-check остаётся привязан к фактическому низу капсулы.
- В конце ролла вызывается `TryStand`: если `CanStand` — обычная стойка; иначе принудительный переход в присед (`IsCrouched = true`, `WantsCrouch = true`) с автоматическим вставанием после освобождения потолка.
- Принудительный присед после ролла не запускает повторный ролл или прыжок и не тратит стамину.
- При прерывании ролла падением высота капсулы восстанавливается по текущей стойке; в воздухе присед сохраняется.
- Направление, скорость, i-frames и кулдаун ролла не меняются.
- Камера: временное уменьшение капсулы в ролле не передаётся в `CrouchAmount`; при возврате в присед pivot опускается штатно.
- `PlayerAnimator`: во время ролла `Crouching` не выставляется из-за временной капсулы; после перехода в присед публикуются `Crouching = true` и `CrouchAmount → 1`.
- Проверка: ролл под низким потолком, ролл из приседа, конец ролла без места для вставания, прерывание падением, авто-вставание, отсутствие застревания и проваливания, регрессия обычного ролла.

Шум, стелс-радиус и отдельные звуки шагов не добавляются.

## Решения и заметки

- Камера: свободная орбита от третьего лица без авто-центрирования (Souls-like).
- Камера в геймплее использует только сырой `Yaw`; сглаживание и инерция — визуальные, иначе запаздывают ходьба, воздушное движение и направление ролла.
- Ввод камеры разделён по устройству: мышь — пиксели за кадр (без `deltaTime`), стик — скорость с `deltaTime`, мёртвой зоной и кривой чувствительности.
- Камера должна быть на верхнем уровне иерархии, не дочерней у игрока, иначе жёсткая привязка позиции даёт джиттер.
- `CharacterController` не поддерживает интерполяцию `Rigidbody`; джиттер лечится порядком обновления (поза камеры в `LateUpdate` после движения) и сглаживанием по Y.
- Плечо и коллизии конфликтуют: при прижатии камеры персонаж уходит из центра кадра; приоритет — сначала дистанция, затем смещение.
- Coyote time живёт в `CharacterMotor`. Grounded-состояния при отрыве сразу уходят в `Fall`, прыжок из `Fall` разрешён только пока coyote активен — так двойной прыжок невозможен.
- В воздухе `airControl` масштабирует разгон/замедление и плавный доворот горизонтального импульса к вводу (`Vector3.Slerp` по `RotationSpeed * airControl`); без ввода направление импульса сохраняется, скорость гасится. На земле и в ролле направление меняется мгновенно.
- Прыжок: импульс `sqrt(2·g·jumpHeight)` считается по базовой гравитации, поэтому высота не зависит от множителей. При падении гравитация × `fallGravityMultiplier`, при отпущенной кнопке на подъёме × `lowJumpGravityMultiplier` — короткое нажатие даёт низкий прыжок, падение ускоряется.
- Ролл без ввода — вперёд; бэкстеп (как в Souls) вне рамок задачи.
- Ролл: в `PlayerMovementConfig` есть `_rollSpeed` (базовая скорость, по умолчанию 8), `rollSpeedCurve` — множитель. Направление camera-relative, фиксируется на входе; для этого в `CharacterMotor` есть `FaceDirection(dir)` и `MoveInstant(dir, speed, dt)` (минуя accel/decel). Ролл приоритетнее прыжка в один кадр, при потере земли прерывается в `Fall`, из coyote-окна недоступен.
- Кулдаун ролла живёт в `PlayerContext` (`RollCooldownRemaining`, `CanRoll`, `StartRollCooldown()`), стартует в `RollState.Enter` и включает время самого ролла. `PlayerContext.IsInvulnerable` — хук для будущего урона, выставляет только `RollState`.
- Jump buffer живёт в `PlayerContext` (`JumpBufferRemaining`, `HasJumpBuffered`, `BufferJump()`, `ConsumeJumpBuffer()`); декремент — в `PlayerContext.Tick`. Каждое новое нажатие перезаписывает таймер. Сброс — в `JumpState.Enter` после успешного `Motor.TryJump`. На приземлении прыжок выполняется на первом `Tick` grounded-состояния.
- `PlayerStamina` — обычный класс (не MonoBehaviour), создаётся внутри `PlayerContext` из `PlayerStaminaConfig`. API: `Current/Max/Normalized`, `IsExhausted`, `CanStartSprint`, `CanContinueSprint`, `CanSpendRoll`, `Tick(dt)`, `DrainSprint(dt)`, `TrySpendRoll()`; событие `Changed(current, max)` для будущего HUD. Конструктор `PlayerContext` принимает `PlayerStaminaConfig` четвёртым параметром, `PlayerCrouchConfig` — дополнительным.
- Спринт не обрывается при падении стамины ниже `sprintMinStamina`, только на нуле; повторный старт требует порога. `_exhausted` снимается только при регенерации до `exhaustedRecoveryThreshold`. Общий `PlayerState.ResolveGroundedState()` учитывает стамину и используется Idle/Walk/Fall/Roll.
- `PlayerAnimator.Tick(PlayerContext, PlayerStateMachine)` вызывается сразу после `FSM.Tick`. `Speed` — фактическая горизонтальная скорость мотора, а не целевая скорость состояния. `Sprinting` — текущее состояние; `Roll` — триггер по фронту входа. `PlayerAnimator.Awake` ставит `applyRootMotion = false`, хэши параметров закэшированы. В Animator `Has Exit Time` выключен у всех переходов, кроме `Roll → Locomotion` — это настройка ассета, код её не контролирует.
- `CharacterMotor` хранит последнее горизонтальное направление `_moveDirection`; при остановке оно не обнуляется, иначе `Deceleration` не тормозит и Blend Tree мгновенно уходит в Idle.
- Земля пересчитывается сразу после `CharacterController.Move` с учётом `CollisionFlags.Below`, поэтому касание видно в том же кадре. `VerticalVelocity` на земле остаётся `-2` для прилипания, а `PlayerAnimator` публикует `VerticalSpeed = 0` при `IsGrounded` — физика и анимация используют разные значения осознанно. `FallState.Tick` вызывает `MoveAir` до повторной проверки земли.
- `CharacterMotor.CheckGround` использует `Physics.SphereCast` вниз из `checkPosition + Vector3.up * GroundCheckRadius` с дистанцией `GroundCheckOffset + GroundCheckRadius` и проверкой `Vector3.Angle(hit.normal, Vector3.up) <= CharacterController.slopeLimit`; стена не считается землёй, ложный `Grounded` при прижатии к стене устранён.
- Захват курсора: `PlayerController.ApplyCursorLock()` вызывается в `Awake`, в `OnApplicationFocus(true)` и по ЛКМ, если `Cursor.lockState != Locked`; Esc отпускает курсор, клик по Game View возвращает захват.
- Причина изменения плана: текущая камера — жёсткая орбита без коллизий и сглаживания, проходит сквозь геометрию, возможен джиттер. Камера напрямую влияет на ощущение управления, поэтому она вынесена в отдельные этапы К1–К6. Лок-он в план не входит.
- Cinemachine в проекте не установлен; для текущего объёма самописная камера из малых этапов проще и предсказуемее.
- `PlayerInputReader`: enum `LookDevice { Mouse, Gamepad }` объявлен в том же файле; устройство определяется по `Look.activeControl` и сохраняется при отсутствии ввода.
- `PlayerCamera` требует `Camera` на том же GameObject (`GetComponent<Camera>()`), сериализованной ссылки на камеру нет.
- `PlayerCamera` без `PlayerCameraConfig` пишет warning и не обновляет позу и FOV.
- `PlayerCameraConfig` объединяет pose/mouse/gamepad/collision/FOV/smoothing в одном SO; поля добавлялись по этапам К1, К3, К4, К6 и 12.
- Значения по умолчанию: `distance = 4.5`, `lookHeight = 1.5`, `shoulderOffset = 0.5`, `pitch −30…55`, `startPitch = 15`, `collisionLayerMask = Default`.
- `Yaw` и `Pitch` камеры отдают сырые углы; визуальная инерция — `SmoothDampAngle` по `_smoothedYaw/_smoothedPitch` с отдельными скоростями.
- Вертикальный pivot — `SmoothDamp`; при упоре в `verticalFollowMaxOffset` накопленная скорость сбрасывается.
- `PlayerCameraCollision.Resolve(pivot, desiredPosition, config, target, deltaTime)` — plain class с общим статическим буфером на 8 хитов, skin `0.02`, `QueryTriggerInteraction.Ignore`, исключением цели и её детей.
- Коллизия сужается мгновенно, расширяется через `MoveTowards` со `collisionReturnSpeed`; `minDistance` работает как нижняя граница, но не больше фактической дистанции до стены.
- При нулевом `collisionRadius` или `collisionLayerMask` коллизии отключены, камера стоит на желаемой дистанции.
- FOV: спринт определяется по `SprintHeld && |Move| > 0.1` без учёта FSM и стамины; импульс ролла — константа `RollFovDuration = 0.25`; FOV применяется в `LateUpdate`.
- Известное ограничение: при движении с зажатым спринтом FOV растёт, даже если спринт заблокирован стаминой; длительность импульса ролла не вынесена в конфиг.
- Фикс воздуха и ground-check: `_airControl` по умолчанию снижен с `0.5` до `0.2`; на существующий `Assets/PlayerMovementConfig.asset` значение по умолчанию не влияет — `Air Control` задаётся в Inspector.
- Причина изменения плана: сейчас игрок двигается только в одной стойке, нет способа пройти в низкие проходы и нет задела под стелс. Присед влияет на ощущение управления и геометрию уровней, поэтому добавлен отдельный блок этапов 9–14; порядок: капсула/потолок → скорость и переходы → камера → анимации → поверхности/уклоны/ступени.
- Присед — ортогональный слой стойки, а не новые FSM-состояния: иначе комбинации с прыжком, падением, роллом и будущим лок-оном размножают состояния. `Crouch.Tick` идёт до `Motor.Tick`, чтобы капсула была изменена до движения и ground-check.
- Режим ввода приседа: toggle по умолчанию, hold доступен настройкой в `PlayerCrouchConfig`. Toggle хранит желаемую стойку; если встать нельзя, игрок остаётся в приседе и встаёт автоматически после освобождения потолка. Повторное нажатие приседа отменяет ожидание вставания.
- Прыжок из приседа: при `CanStand` присед снимается и прыжок выполняется в том же кадре; если места нет — прыжок игнорируется, jump buffer сбрасывается, чтобы не сработал позже.
- Радиус капсулы при приседе не меняется: только `height`/`center`, низ капсулы фиксируется относительно `transform.position`. Это уменьшает выталкивания из стен и проваливание. `stepOffset` ограничивается безопасной величиной при низкой капсуле.
- Проверка потолка: измеряется свободная высота вверх (`CheckCapsule`/`CapsuleCast`, слой потолка, исключение игрока), рост капсулы не превышает её. Ground-check привязан к фактическому низу капсулы, поэтому переходный кадр не даёт ложный `Fall`.
- Спринт: вход в присед отменяет спринт, в приседе спринт игнорируется; после вставания спринт возобновляется только при удержании кнопки и достаточной стамине. Перекат: при `CanStand` встаём и перекатываемся, иначе перекат запрещён; направление, i-frames и кулдаун не меняются. В воздухе из приседа стойка сохраняется до приземления.
- Камера приседа: `crouchLookHeight` в `PlayerCameraConfig`, целевая высота pivot интерполируется по `CrouchAmount` и проходит через существующий `FollowPivotHeight`. Второе сглаживание не добавляется, коллизии камеры и их радиус не меняются.
- Анимации: `PlayerAnimator` публикует `Crouching` и `CrouchAmount`; `CrouchAmount` синхронизируется с фактической высотой капсулы, а не с bool. `Speed` остаётся фактической горизонтальной скоростью, `Sprinting` в приседе `false`. Blend tree и переходы — ручная настройка Animator-ассета.
- Лок-он в план по-прежнему не входит; присед не меняет yaw/фейсинг, чтобы будущий лок-он мог управлять поворотом независимо.
- Шум, стелс-радиус и отдельные звуки шагов исключены по решению: задел под ИИ пока не нужен.
- `PlayerCrouchConfig` вынесен в отдельный SO по аналогии со стаминой и камерой. `PlayerContext` принимает его дополнительным параметром; Input Actions, Animator и новый SO-ассет настраиваются в Unity вручную (ассеты кодом не редактируются).
- Этап 9: в `PlayerInputReader` добавлены `_crouchAction`, `CrouchPressed`, `CrouchHeld`; действие `Crouch` и SO-ассет приседа настраиваются в Unity вручную.
- Этап 10: конструктор `PlayerCrouchController` расширен до `(PlayerInputReader, PlayerCrouchConfig, CharacterController, Transform)`; `PlayerContext` передаёт `motor.Controller` и `playerTransform`.
- Этап 10: в `CharacterMotor` добавлен публичный геттер `Controller` для доступа слоя стойки к `CharacterController`.
- Этап 10: центр капсулы выводится из зафиксированного низа (`center.y = standBottom + currentHeight / 2`); `crouchCenterY` ограничивает целевой рост, а не задаёт центр напрямую.
- Этап 10: свободная высота измеряется `Physics.SphereCastNonAlloc` (буфер 16) вверх от низа капсулы; радиус луча уменьшен на `ceilingSkin`, старт приподнят на `ceilingSkin`, коллайдеры игрока и его детей исключаются.
- Этап 10: `CanStand` = свободная высота не меньше ростовой `standHeight` с допуском `0.001`; рост капсулы клампится свободной высотой, `stepOffset` — это `min(desired, standStepOffset, currentHeight / 2)`.
- Этап 10: `CrouchAmount` считается из фактической высоты капсулы по формуле `(standHeight − currentHeight) / (standHeight − crouchTargetHeight)`.
- Этап 10: добавлен публичный `FreeHeight`; лог `PlayerController` дополнен `FreeH` и `CrouchAmount`.
- Этап 10 не потребовал новых полей `PlayerCrouchConfig`; конфиг-ассет менять не нужно.
- Этап 11: `CharacterMotor.Move` получил необязательные `acceleration`/`deceleration` (−1 — брать из `PlayerMovementConfig`); существующие вызовы не меняются.
- Этап 11: `PlayerState.MoveInternal` при приседе подменяет целевую скорость на `CrouchSpeed`, а ускорение/замедление — на `CrouchAcceleration`/`CrouchDeceleration`; `Stop` использует те же rates при нулевой цели. Скорость в воздухе при приседе — `CrouchSpeed`, спринт игнорируется.
- Этап 11: `PlayerCrouchController` получил `Config` и `TryStand()`; при неудаче (потолок) возвращает `false`. `TryStand` сбрасывает `WantsCrouch`, чтобы toggle-режим не возвращал присед.
- Этап 11: `ResolveGroundedState` при приседе возвращает только Idle/Walk; `SprintState` выходит в `Walk` при приседе, `WalkState` не стартует спринт в приседе.
- Этап 11: прыжок и ролл сначала вызывают `TryStand()`; при запрете прыжка `JumpBuffer` сбрасывается, ролл просто не начинается. Direction/i-frames/кулдаун ролла не менялись.
- Этап 12: в `PlayerCameraConfig` добавлен `crouchLookHeight` (по умолчанию 1.0). `PlayerCamera.SetCrouchAmount` принимает `CrouchAmount`, целевая высота pivot — `Mathf.Lerp(LookHeight, CrouchLookHeight, ...)`.
- Этап 12: `PlayerController` передаёт `CrouchAmount` камере сразу после `Crouch.Tick`; второе сглаживание не добавлено — работает существующий `FollowPivotHeight`. Коллизия камеры и её маска не менялись.
- Этап 13: `PlayerAnimator` публикует `Crouching` (`IsCrouched`) и `CrouchAmount` (фактическая высота капсулы); `Speed` остаётся `HorizontalSpeed`, `Sprinting` дополнительно гасится при приседе. Blend tree и переходы настраиваются в Animator-ассете вручную.
- Animator: `Crouching` добавлен как Bool, но в 2D-дереве не используется — нужен только чтобы `SetBool` не писал warning.
- Animator: `Locomotion` — 2D Freeform Cartesian (`X = Speed`, `Y = CrouchAmount`), точки Idle (0,0), Standard Walk (4.5,0), Running (7,0), Crouching Idle (0,1), Crouch Walk Forward (2.5,1).
- Animator: `Sprinting` не участвует в 2D-дереве, спринт различается по `Speed = SprintSpeed`; в приседе bool всегда `false`.
- Animator: `Standing To Crouched`/`Crouched To Standing` импортированы, но в базовом слое не используются — их длительность не совпадает с быстрым изменением капсулы (`HeightTransitionSpeed`).
- Animator: в воздухе при приседе играет обычный Fall/Jump клип; `CrouchAmount` и капсула сохраняются, crouch-вариантов air-клипов нет.
- Отзыв проверки этапа 13: работает, критичных проблем нет; замечание — при ролле капсулу нужно уменьшать так же, как при приседе.
- Этап 15 добавлен по отзыву: капсула на ролле уменьшается как при приседе; если в конце ролла встать нельзя, игрок принудительно переходит в присед.
- Этап 14: отдельной логики скольжения/ступеней не добавлено — поведение задаёт `CharacterController` (`slopeLimit`, `stepOffset`). Присед меняет только `height`/`center` при фиксированном низе и радиусе, поэтому нижняя полусфера капсулы и контакт со склонами/ступенями не меняются.
- Этап 14: расчёт `stepOffset` вынесен в `PlayerCrouchController.ApplyStepOffset()`. Значение всегда ограничено `min(_standStepOffset, height * 0.5 - StepSafetyMargin)`; запас `0.01` не даёт упереться в границу `height / 2`, из-за которой низкая капсула может «залезать» на ступени и проваливаться. Новых полей `PlayerCrouchConfig` не потребовалось: ступени в приседе тюнингуются существующим `CrouchStepOffset`.
- Этап 14: добавлены публичные `StandHeight`, `CurrentHeight`, `StepOffset`; в лог `PlayerController` добавлены `CH` (фактическая высота капсулы) и `Step` (текущий `stepOffset`) для проверки ступеней, низких проходов и краёв.
- Этап 14: проверка в редакторе — ступени 0.2–0.3 в приседе (подъём/спуск), склоны ниже и выше `slopeLimit`, края платформ, низкий проход; регрессия спринта, ролла, прыжка, камеры и ground-check.
- Отзыв проверки этапа 14: работает, критичных проблем и замечаний по ощущениям/коду нет.
- Этап 15: в `PlayerCrouchController` добавлен временный модификатор капсулы ролла — `BeginRollCapsule`/`EndRollCapsule`/`CancelRollCapsule`. На время ролла цель по высоте — `min(стойка, crouchTargetHeight)` при фиксированном низе, `IsCrouched`/`WantsCrouch` не меняются; `stepOffset` считается как при низкой капсуле (`CrouchStepOffset` + ограничение по высоте).
- Этап 15: `CrouchAmount` теперь ведёт стойка со скоростью `HeightTransitionSpeed` (0 ↔ 1), а не фактическая высота капсулы. В обычном приседе значения совпадают (оба идут с одной скоростью от одного старта), но временное сжатие в ролле и обратный рост капсулы больше не опускают pivot камеры и не переключают Blend Tree.
- Этап 15: в конце ролла `RollState` вызывает `EndRollCapsule`; при `!CanStand` срабатывает `ForceCrouch` (`IsCrouched = true`, `WantsCrouch = true`). Авто-вставание после освобождения потолка делает флаг `_forcedCrouch`: в Toggle он снимается сам при `CanStand`, в Hold — только после отпускания кнопки; повторное нажатие приседа в Toggle отменяет принудительный присед и пытается встать.
- Этап 15: `RollState.Exit` вызывает `CancelRollCapsule`, поэтому прерывание ролла падением снимает только временный модификатор — капсула восстанавливается по текущей стойке, принудительного приседа нет. Направление, скорость, i-frames и кулдаун ролла не менялись, стамина списывается только в `Enter`.
