# Текущий прогресс

Статус проекта в 40–60 строк. Подробности по задачам — `docs/session-logs/`, план — `docs/roadmap.md`, ручные шаги — `docs/manual-queue.md`.

## Статус
- Unity 6 / URP. Движение игрока готово полностью.
- Текущий этап: первое существо — наземный динозавр-хищник.
- Выполнено и проверено: DINO-001 … DINO-007. Следующая задача — **DINO-008** (`CreatureTargetMemory`).

## Игрок
- `CharacterController` + новый Input System; FSM Idle/Walk/Sprint/Jump/Fall/Roll.
- Jump buffer, coyote time, выносливость, приседание, камера от третьего лица.
- Animator-параметры публикуются через `PlayerAnimator`.
- Системы здоровья нет — контракт урона не согласован (DINO-021).

## Существо
| Задача | Компонент | Состояние |
| --- | --- | --- |
| DINO-002 | `CreatureConfig` | класс готов, ассет `Assets/Configs/CreatureConfig.asset` создан |
| DINO-003 | `CreatureNavigator` | готов, `IsPathActive` исправлен |
| DINO-004 | `CreatureController` | готов, `_homePosition` фиксируется в `Start` |
| DINO-005 | `CreatureStateMachine`, `IdleState` | готово |
| DINO-006 | `ReturnState`, `RequestReturn()` | работает, проверено в Play Mode |
| DINO-007 | `CreatureAnimator` (`Speed`) | публикует скорость; анимации движения не проверены |

- Восприятия (зрение/слух), памяти о цели и атаки нет.
- NavMesh запечён (`Assets/Scenes/SampleScene/NavMesh-Plane.asset`), агент на сетке, возврат к домашней точке проезжает ~11 м.
- Текущее поведение существа: стоять в `Idle` и по клавише `R` вернуться к домашней точке.
- Отладочный компонент пользователя — `Assets/CreatureDebuger.cs` (`CreatureDebugInput`).

## Готовые соглашения
- Animator существа: параметр `Speed` (float, м/с), стейты `idle`/`walk`/`run`.
- Слои: 3 `Creature`, 6 `Player`, 7 `obstacle`. Подробнее — `project-context.md`.

## Ближайшие шаги
1. DINO-008: память о цели.
2. DINO-009 … DINO-011: зрение и первый переход Idle → Investigate/Chase.
3. Пользователь: проверить анимации движения (пункт DINO-007 в `manual-queue.md`).

## Ограничения
- Агент не правит Unity-ассеты, не запускает сборку/тесты.
- Связь с пользователем — только через `manual-queue.md`.
