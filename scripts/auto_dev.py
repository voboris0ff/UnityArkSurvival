#!/usr/bin/env python3
"""Run roadmap tasks one at a time in fresh OpenCode CLI sessions.

Requires:
  - Python 3.10+
  - OpenCode CLI available as `opencode`
  - Run from the Unity project root (the directory containing AGENTS.md)

Selection rules (must match docs/roadmap.md):
  - `[ ]` pending — candidate task
  - `[x]` done — skipped
  - `[!]` waiting for the user — skipped, does not stop the run
The orchestrator trusts the roadmap status written by the agent.
"""
from __future__ import annotations

import argparse
import datetime as dt
import re
import subprocess
import sys
import os
import shutil
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
TASK_RE = re.compile(
    r"^\s*-\s*\[(?P<status>[ x!])\]\s+.*?\*\*(?P<id>[A-Z]+-\d+)\*\*"
)


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

def is_task_done(task_id: str) -> bool:
    return _task_status(task_id) == "x"

def is_task_blocked(task_id: str) -> bool:
    return _task_status(task_id) == "!"

def _task_status(task_id: str) -> str:
    for line in ROADMAP.read_text(encoding="utf-8").splitlines():
        match = TASK_RE.match(line)

        if match and match.group("id") == task_id:
            return match.group("status")

    return ""

def build_prompt(task: dict[str, str]) -> str:
    return f"""Выполни ровно одну задачу roadmap: {task['id']}.

Правила:
- Найди точную задачу {task['id']} в docs/roadmap.md и выполняй только её.
- Прочитай AGENTS.md и переданные файлы контекста.
- Не выполняй сборку, тесты, Unity Editor, сложные проверки, git commit/push.
- Не редактируй Unity Assets и запрещённые типы файлов (чтение для анализа разрешено).
- Не начинай соседние задачи.
- Не задавай вопросы в чате: единственный канал общения — docs/manual-queue.md с блоком `Ответ пользователя:`.
- Сначала разбери существующие `Ответ пользователя:` в docs/manual-queue.md: если ответ достаточен — удали пункт и обнови статусы; если нет — допиши блок `Вопрос агенту:` и пометь задачу `[!]` в roadmap.
- Если не хватает имени (Animator-параметр, слой, поле) — придумай его сам, занеси в «Соглашения об именах» в docs/project-context.md и добавь ручной шаг в docs/manual-queue.md. Это не блокирует задачу.
- Соблюдай лимиты объёма: docs/progress.md 40–60 строк, лог сессии не больше ~20 строк.
- Обнови docs/roadmap.md, docs/progress.md и docs/manual-queue.md.
- Создай краткий лог в docs/session-logs/ с ID задачи, изменёнными файлами, результатом и ручными действиями.
- Не переписывай большие документы целиком без необходимости.
- Не делай широкого аудита проекта. Изучи только файлы, необходимые для этой задачи.
- Меняй [ ] на [x] только после полного выполнения задачи.
- Если для задачи физически не хватает решения пользователя, поставь [!] и всё равно добавь пункт в docs/manual-queue.md.
- Не изменяй статусы других задач.

В конце выведи:
TASK_RESULT: DONE / BLOCKED / FAILED
TASK_SCORE: число от 0 до 100

TASK_SCORE должен быть последней строкой ответа.
"""


def run_task(task: dict[str, str], timeout: int) -> tuple[bool, str]:
    prompt = build_prompt(task)

    opencode = shutil.which("opencode.cmd") or shutil.which("opencode")

    if not opencode:
        return False, "OpenCode CLI не найден. Установи CLI или добавь `opencode` в PATH."

    command = [
    opencode,
    "run",
    "--model",
    "a6api/deepseek-v4.1-flash-1017",
]

    for relative in CONTEXT_FILES:
        command.extend(["--file", relative])

    command.append(prompt)

    # Windows не запускает .cmd напрямую через subprocess.
    if os.name == "nt" and opencode.lower().endswith((".cmd", ".bat")):
        command = ["cmd.exe", "/c"] + command

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
        output = exc.stdout or ""
        if isinstance(output, bytes):
            output = output.decode("utf-8", errors="replace")
        output += f"\n[ORCHESTRATOR] Timeout after {timeout} seconds."
        return False, output

    except FileNotFoundError as exc:
        return False, f"Не удалось запустить OpenCode: {exc}"

    print(output[-4000:], flush=True)

    score_matches = re.findall(
        r"(?m)^TASK_SCORE:\s*(100|[1-9]?\d)\s*$",
        output
    )

    score = int(score_matches[-1]) if score_matches else None

    done_marker = "TASK_RESULT: DONE" in output

    success = result.returncode == 0 and done_marker and score is not None

    if score is None:
        output += "\n[ORCHESTRATOR] Агент не предоставил оценку TASK_SCORE."

    return success, output


def save_log(task_id: str, output: str, result: str) -> None:
    """Краткий лог сессии. Полный вывод OpenCode не сохраняется — он раздувает контекст."""
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    summary = output.rstrip().splitlines()[-15:]
    excerpt = "\n".join(summary)
    path = LOG_DIR / f"{stamp}-{task_id}.md"
    path.write_text(
        f"# Сессия {task_id}\n\n"
        f"- Время: {dt.datetime.now().isoformat(timespec='seconds')}\n"
        f"- Результат оркестратора: {result}\n\n"
        f"## Хвост вывода\n\n```text\n{excerpt}\n```\n",
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
        match = re.search(r"^tasks_per_run:\s*(\d+)", config_text, re.MULTILINE)
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

        if is_task_done(task["id"]):
            # Агент мог отметить задачу [x], но упасть до выхода: просто идём дальше.
            completed += 1
            continue

        success, output = run_task(task, args.timeout)
        roadmap_done = is_task_done(task["id"])

        status = "DONE" if roadmap_done else "NOT_DONE"
        save_log(task["id"], output, status)

        if roadmap_done:
            completed += 1
            print(
                f"[ORCHESTRATOR] {task['id']} подтверждена через roadmap "
                f"({completed}/{limit or 'all'})."
            )
            continue

        # Задача не выполнена: допустимо, если агент пометил её как ожидающую пользователя.
        if is_task_blocked(task["id"]):
            print(
                f"[ORCHESTRATOR] {task['id']} ждёт ответа пользователя в docs/manual-queue.md. "
                "Продолжаю со следующей задачей."
            )
            continue

        print(
            f"[ORCHESTRATOR] {task['id']} не отмечена выполненной в roadmap. "
            "Останавливаюсь."
        )
        print("Проверь roadmap, manual-queue.md и лог сессии.")
        break

    print(f"Готово. Успешно завершено задач: {completed}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
