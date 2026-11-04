# Unity Auto Dev

## Installation
1. Copy the archive contents into the Unity project root, or move the files to the matching paths.
2. Make sure the `opencode` command works from a terminal in the project root.
3. Check that `AGENTS.md` and the `docs/` folder are in the project root.
4. Run:

```bash
python scripts/auto_dev.py --tasks 3
```

Other variants:

```bash
python scripts/auto_dev.py --tasks 10
python scripts/auto_dev.py --tasks all
python scripts/auto_dev.py --tasks 2 --timeout 1800
```

The default number of tasks comes from `auto-dev.yaml`.

## How communication with the user works
- The only channel is `docs/manual-queue.md`.
- Every item has an `Ответ пользователя:` block where the user writes the result. That file stays in Russian.
- At the start of a session the agent processes the answers: a sufficient answer → the item is deleted and the task status is updated; an insufficient one → `Вопрос агенту:` is appended to the item, the task is marked `[!]`, and work continues with the next task.
- `[!]` tasks do not stop the orchestrator.

## Important limitations of the current prototype
- Run from the Unity project root.
- The orchestrator takes the first task with status `[ ]`; `[x]` and `[!]` are skipped.
- Each task runs as a separate `opencode run` command, i.e. in a fresh context.
- It is enough for the agent to set `[x]` in the roadmap and print `TASK_RESULT: DONE`; `TASK_SCORE` is only checked for presence.
- If a task is still `[ ]` and not marked `[!]`, execution stops so that errors are not multiplied.
- The session log is a short output tail (~15 lines); the full output is not saved.
- The prototype is not transactional: back up the project before the first run or use Git locally.
