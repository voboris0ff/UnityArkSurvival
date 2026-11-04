# AGENTS.md

Single source of rules. Other documents reference it and do not duplicate the rules.

## Project
- Unity 6000.0.85f1, URP 17.0.4, Input System 1.20.0 (do not use the Legacy Input Manager), AI Navigation 2.0.15.
- Scripts: C# in `Assets/Scripts/`. Namespace follows the folder: `Assets/Scripts/AI` → `ArkSurvival.AI`.

## C#
- Public members `PascalCase`, private fields `_camelCase`; `[SerializeField] private` instead of public fields.
- One class per file, file name = class name.
- Cache references in `Awake`/`Start`; no `Find`/`FindObjectOfType`/`GetComponent` in `Update`.
- No unnecessary abstractions and no new dependencies.

## Forbidden
- Writing to `.unity`, `.prefab`, `.meta`, `.asset`, `.mat`, `.anim`, `.controller`. Reading for analysis is allowed.
- Creating `.meta`, changing packages, git commit/push.
- Running the build, PlayMode/EditMode tests, long checks.
- Claiming that code was verified by compilation or in the editor when it was not.

## Undefined names
- A new name outside code (Animator parameter, layer, tag, animation event, menu): pick a meaningful one, record it in "Naming Conventions" in `docs/project-context.md` and add an item to `docs/manual-queue.md`. Names in code live in the code and are not duplicated in documents.
- Balance numbers (speeds, radii, damage): a start value marked "temporary"; tuning is up to the user.
- Blocking (`[!]`) only when the task cannot be done without a user decision. Then: an item in `manual-queue.md`, status `[!]`, stop working on the task.

## Communication with the user
- The only channel is `docs/manual-queue.md`. No questions in chat: the user will not see them.
- The orchestrator passes filled `Ответ пользователя:` blocks in the prompt. If there are none, do not read the file until you need to add an item.
- Answer sufficient → delete the item entirely and update the task status in `roadmap.md`.
- Answer incomplete → append one specific `Вопрос агенту:` to the item, mark the task `[!]`.
- Every new manual step is an item following the template below.
- **Language:** the user speaks Russian, so `docs/manual-queue.md` is written in Russian (headings, steps, questions). All other documentation is in English.

```md
### DINO-XXX — короткий заголовок
- **Что сделать:** шаги.
- **Где:** путь/меню редактора.
- **Зачем:** причина.
- **Готово, когда:** измеримый результат.
- **Ответ пользователя:**
  <!-- пользователь пишет здесь; агент удаляет пункт после обработки -->
```

## Workflow
- One roadmap task per session. Do not do neighbouring tasks "along the way", do not change other statuses.
- Edits are minimal and targeted, only the files of the task.
- Read only what you need: your task line in `roadmap.md`, `progress.md` and `project-context.md` — only if unavoidable. No project audit. Do not re-read files you just changed.
- Large edit (many files or a contract change): raise a question in `manual-queue.md`, mark the task `[!]`. Waiting for a chat confirmation is not allowed.

## Task completion
1. `docs/roadmap.md`: `[x]` — done, `[!]` — waiting for the user. The status changes in place, the task is not deleted; moving it to `docs/roadmap-archive.md` is done by the orchestrator.
2. `docs/progress.md`: targeted edits of the "Done" and "Next task" lines. Do not add implementation details.
3. `docs/manual-queue.md`: only new items and removal of processed ones.
4. Do not write a session log: the orchestrator creates it.
5. Final answer — one short line about what was done.

## Document sizes (in characters, not lines)
- `docs/progress.md` — up to ~3 000 characters: status, current and next task, a short list of ready systems (one line per system), links. No per-task tables: history is in `docs/session-logs/` and `roadmap.md`.
- `docs/roadmap.md` — one line per task, up to ~200 characters. Details are in the code and `project-context.md`.
- `docs/manual-queue.md` — open items only. Accepted agreements move to `project-context.md`.
- `docs/project-context.md` — 6 000–7 000 characters. Only what is not visible from code: names and contracts on the Unity-asset/code boundary (Animator parameters, layers, tags, inspector fields, animation events), contracts between systems (frame order, transition table, priorities, interface and event signatures), user decisions. Forbidden: describing class logic, task history by number, copies of balance numbers (instead — the field name and class).
