#!/usr/bin/env python3
"""Run roadmap tasks one at a time in fresh OpenCode CLI sessions.

Requires:
  - Python 3.10+
  - OpenCode CLI available as `opencode`
  - Run from the Unity project root (the directory containing AGENTS.md)
"""
from __future__ import annotations

import argparse
import datetime as dt
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROADMAP = ROOT / "docs" / "roadmap.md"
LOG_DIR = ROOT / "docs" / "session-logs"
CONTEXT_FILES = [
    "AGENTS.md",
    "docs/project-context.md",
    "docs/progress.md",
    "docs/roadmap.md",
    "docs/manual-queue.md",
]
TASK_RE = re.compile(r"^(?P<prefix>\s*-\s*)\\[(?P<status>[ x!])\\](?P<rest>.*\\*\\*(?P<id>[A-Z]+-\\d+)\\*\\*.*)$")


def parse_tasks(text: str) -> list[dict[str, str]]:
    tasks = []
    for line_number, line in enumerate(text.splitlines()):
        match = TASK_RE.match(line)
        if match and match.group("status") == " ":
            tasks.append({
                "id": match.group("id"),
                "line_number": str(line_number),
                "line": line,
            })
    return tasks


def mark_task_done(task_id: str) -> None:
    lines = ROADMAP.read_text(encoding="utf-8").splitlines()
    for index, line in enumerate(lines):
        if f"**{task_id}**" in line and line.lstrip().startswith("- [ ]"):
            lines[index] = line.replace("- [ ]", "- [x]", 1)
            ROADMAP.write_text("\n".join(lines) + "\n", encoding="utf-8")
            return


def build_prompt(task: dict[str, str]) -> str:
    return f"""Выполни ровно одну задачу roadmap: {task['id']}.

Правила:
- Найди точную задачу {task['id']} в docs/roadmap.md и выполняй только её.
- Прочитай AGENTS.md и переданные файлы контекста.
- Не выполняй сборку, тесты, Unity Editor, сложные проверки, git commit/push.
- Не редактируй Unity Assets и запрещённые типы файлов.
- Не начинай соседние задачи.
- Если нужны действия пользователя в Unity, подробно добавь их в docs/manual-queue.md.
- Если задача заблокирована неизвестным решением или ручным действием, не помечай её выполненной.
- Обнови docs/roadmap.md, docs/progress.md и docs/manual-queue.md.
- Создай краткий лог в docs/session-logs/ с ID задачи, изменёнными файлами, результатом и ручными действиями.
- Не переписывай большие документы целиком без необходимости.
- Не делай широкого аудита проекта. Изучи только файлы, необходимые для этой задачи.
- В конце выведи ровно одну отдельную строку:
  TASK_RESULT: DONE
  либо TASK_RESULT: BLOCKED
  либо TASK_RESULT: FAILED
Строка DONE допустима только если задача действительно выполнена и её статус обновлён в roadmap.
"""


def run_task(task: dict[str, str], timeout: int) -> tuple[bool, str]:
    prompt = build_prompt(task)
    command = ["opencode", "run"]
    for relative in CONTEXT_FILES:
        command.extend(["--file", relative])
    command.append(prompt)

    print(f"\n=== {task['id']} ===", flush=True)
    try:
        result = subprocess.run(
            command,
            cwd=ROOT,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=timeout,
            encoding="utf-8",
            errors="replace",
        )
        output = result.stdout or ""
    except subprocess.TimeoutExpired as exc:
        output = (exc.stdout or "")
        if isinstance(output, bytes):
            output = output.decode("utf-8", errors="replace")
        output += f"\n[ORCHESTRATOR] Timeout after {timeout} seconds."
        return False, output
    except FileNotFoundError:
        return False, "OpenCode CLI не найден. Установи CLI или добавь `opencode` в PATH."

    print(output[-12000:], flush=True)
    done_marker = "TASK_RESULT: DONE" in output
    return result.returncode == 0 and done_marker, output


def save_log(task_id: str, output: str, result: str) -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    safe_output = output[-16000:]
    path = LOG_DIR / f"{stamp}-{task_id}.md"
    path.write_text(
        f"# Сессия {task_id}\n\n"
        f"- Время: {dt.datetime.now().isoformat(timespec='seconds')}\n"
        f"- Результат оркестратора: {result}\n\n"
        f"## Вывод OpenCode\n\n```text\n{safe_output}\n```\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Run roadmap tasks in fresh OpenCode sessions.")
    parser.add_argument("--tasks", default=None, help="Количество задач или all.")
    parser.add_argument("--timeout", type=int, default=1200, help="Таймаут одной сессии в секундах.")
    args = parser.parse_args()

    if not ROADMAP.exists():
        print(f"Не найден {ROADMAP}", file=sys.stderr)
        return 2
    for relative in CONTEXT_FILES:
        if not (ROOT / relative).exists():
            print(f"Не найден файл контекста: {relative}", file=sys.stderr)
            return 2

    configured_count = 5
    try:
        config_text = (ROOT / "auto-dev.yaml").read_text(encoding="utf-8")
        match = re.search(r"^tasks_per_run:\s*(\\d+)", config_text, re.MULTILINE)
        if match:
            configured_count = int(match.group(1))
    except OSError:
        pass

    limit = None if args.tasks == "all" else int(args.tasks) if args.tasks else configured_count
    if limit is not None and limit < 1:
        parser.error("--tasks должен быть положительным числом или all")

    completed = 0
    while limit is None or completed < limit:
        tasks = parse_tasks(ROADMAP.read_text(encoding="utf-8"))
        if not tasks:
            print("Невыполненных задач не осталось.")
            break

        task = tasks[0]
        success, output = run_task(task, args.timeout)
        status = "DONE" if success else "NOT_DONE"
        save_log(task["id"], output, status)

        if success:
            # Safety net: agent should update the roadmap itself; orchestrator confirms it.
            mark_task_done(task["id"])
            completed += 1
            print(f"[ORCHESTRATOR] {task['id']} завершена ({completed}/{limit or 'all'}).")
        else:
            print(f"[ORCHESTRATOR] {task['id']} не подтверждена как DONE. Останавливаюсь.")
            print("Проверь roadmap, manual-queue.md и лог сессии.")
            break

    print(f"Готово. Успешно завершено задач: {completed}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
