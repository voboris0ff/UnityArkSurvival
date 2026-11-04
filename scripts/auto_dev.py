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

Token budget rules:
  - AGENTS.md is loaded by OpenCode itself; it is NOT passed again via --file.
  - docs/roadmap.md, progress.md, manual-queue.md, project-context.md are read by
    the agent on demand, not attached on every run.
  - Only the single task line and its stage title are sent with the prompt.
  - The agent `autodev` (opencode.json) pins steps, model and permissions.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import subprocess
import sys
import os
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROADMAP = ROOT / "docs" / "roadmap.md"
CONFIG = ROOT / "auto-dev.yaml"
LOG_DIR = ROOT / "docs" / "session-logs"
AGENT = "autodev"
DEFAULT_MODEL = "a6api/deepseek-v4.1-flash-1017#low"
# Files the prompt refers to. They must exist, but are not attached to the request.
REQUIRED_FILES = [
    "AGENTS.md",
    "docs/project-context.md",
    "docs/progress.md",
    "docs/roadmap.md",
    "docs/manual-queue.md",
]
STAGE_RE = re.compile(r"^##\s+(?P<title>.+)$")
TASK_RE = re.compile(
    r"^\s*-\s*\[(?P<status>[ x!])\]\s+.*?\*\*(?P<id>[A-Z]+-\d+)\*\*"
)


def parse_tasks(text: str) -> list[dict[str, str]]:
    """Return pending tasks with their stage title (to avoid reading the whole roadmap)."""
    tasks = []
    stage = ""

    for line_number, line in enumerate(text.splitlines()):
        stage_match = STAGE_RE.match(line)

        if stage_match:
            stage = stage_match.group("title").strip()
            continue

        match = TASK_RE.match(line)

        if match and match.group("status") == " ":
            tasks.append({
                "id": match.group("id"),
                "stage": stage,
                "line_number": str(line_number),
                "line": line.strip(),
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
    """Short prompt: the rules live in AGENTS.md, which OpenCode loads automatically."""
    return f"""Выполни ровно одну задачу roadmap {task['id']} (этап: {task['stage']}).

Строка задачи в docs/roadmap.md:
{task['line']}

Следуй правилам AGENTS.md. Прочитай AGENTS.md, затем только нужные для задачи файлы
(docs/manual-queue.md разбери первым, docs/roadmap.md — только строка этой задачи,
docs/progress.md и docs/project-context.md — по необходимости). Без широкого аудита проекта,
без сборки, тестов, Unity Editor, git commit/push, без правки Unity-ассетов.
Не начинай соседние задачи и не меняй статусы других задач.
Каждый новый ручной шаг — пункт в docs/manual-queue.md с пустым блоком `Ответ пользователя:`.

В конце ответа — одна строка, ровно в таком виде:
TASK_RESULT: DONE
"""


def load_settings() -> dict[str, object]:
    """Read scripts settings from auto-dev.yaml without an extra dependency."""
    settings: dict[str, object] = {
        "tasks_per_run": 5,
        "timeout": 1200,
        "max_attempts": 2,
        "model": DEFAULT_MODEL,
    }
    try:
        text = CONFIG.read_text(encoding="utf-8")
    except OSError:
        return settings

    scalars = {
        "tasks_per_run": ("tasks_per_run", int),
        "timeout_minutes": ("timeout", lambda value: int(value) * 60),
        "max_attempts_per_task": ("max_attempts", int),
        "model": ("model", str),
    }
    for key, (target, cast) in scalars.items():
        match = re.search(rf"^\s*{key}:\s*(\S+)", text, re.MULTILINE)
        if match:
            try:
                settings[target] = cast(match.group(1).strip("\"'"))
            except ValueError:
                pass

    return settings


def build_command(prompt: str, model: str) -> list[str]:
    opencode = shutil.which("opencode.cmd") or shutil.which("opencode")

    if not opencode:
        return []

    command = [
        opencode,
        "run",
        "--agent",
        AGENT,
        "--model",
        model,
        prompt,
    ]

    # Windows не запускает .cmd напрямую через subprocess.
    if os.name == "nt" and opencode.lower().endswith((".cmd", ".bat")):
        command = ["cmd.exe", "/c"] + command

    return command


def run_task(task: dict[str, str], timeout: int, model: str) -> tuple[bool, str]:
    prompt = build_prompt(task)
    command = build_command(prompt, model)

    if not command:
        return False, "OpenCode CLI не найден. Установи CLI или добавь `opencode` в PATH."

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

    print(output[-2000:], flush=True)

    success = result.returncode == 0 and "TASK_RESULT: DONE" in output

    return success, output


def save_log(task_id: str, output: str, result: str, attempt: int) -> None:
    """Краткий лог сессии. Полный вывод OpenCode не сохраняется — он раздувает контекст."""
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    summary = output.rstrip().splitlines()[-10:]
    excerpt = "\n".join(summary)[-1500:]
    path = LOG_DIR / f"{stamp}-{task_id}.md"
    path.write_text(
        f"# Сессия {task_id} (попытка {attempt})\n\n"
        f"- Время: {dt.datetime.now().isoformat(timespec='seconds')}\n"
        f"- Результат оркестратора: {result}\n\n"
        f"## Хвост вывода\n\n```text\n{excerpt}\n```\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Run roadmap tasks in fresh OpenCode sessions.")
    parser.add_argument("--tasks", default=None, help="Количество задач или all.")
    parser.add_argument("--timeout", type=int, default=None, help="Таймаут одной сессии в секундах.")
    parser.add_argument("--model", default=None, help="Переопределить модель (provider/model#variant).")
    parser.add_argument("--dry-run", action="store_true", help="Показать команду и промпт, ничего не запускать.")
    args = parser.parse_args()

    if not ROADMAP.exists():
        print(f"Не найден {ROADMAP}", file=sys.stderr)
        return 2
    for relative in REQUIRED_FILES:
        if not (ROOT / relative).exists():
            print(f"Не найден файл контекста: {relative}", file=sys.stderr)
            return 2

    settings = load_settings()
    timeout = args.timeout or int(settings["timeout"])
    model = args.model or str(settings["model"])
    max_attempts = max(1, int(settings["max_attempts"]))
    configured_count = int(settings["tasks_per_run"])

    if args.tasks == "all":
        limit = None
    else:
        try:
            limit = int(args.tasks) if args.tasks else configured_count
        except ValueError:
            parser.error("--tasks должен быть числом или all")

    if limit is not None and limit < 1:
        parser.error("--tasks должен быть положительным числом или all")

    if args.dry_run:
        tasks = parse_tasks(ROADMAP.read_text(encoding="utf-8"))
        if not tasks:
            print("Невыполненных задач не осталось.")
            return 0
        command = build_command(build_prompt(tasks[0]), model)
        print(json.dumps({"command": command, "agent": AGENT, "timeout": timeout,
                          "max_attempts": max_attempts, "limit": limit},
                         ensure_ascii=False, indent=2))
        return 0

    completed = 0
    skipped: set[str] = set()

    while limit is None or completed < limit:
        tasks = parse_tasks(ROADMAP.read_text(encoding="utf-8"))
        # Блокированные и упавшие задачи исключаем, иначе выборка берёт ту же задачу снова.
        task = next((item for item in tasks if item["id"] not in skipped), None)
        if task is None:
            print("Невыполненных задач не осталось.")
            break

        roadmap_done = False

        for attempt in range(1, max_attempts + 1):
            success, output = run_task(task, timeout, model)

            if is_task_done(task["id"]):
                roadmap_done = True
                save_log(task["id"], output, "DONE", attempt)
                break

            # Задача не выполнена: агент мог пометить её как ожидающую пользователя.
            if is_task_blocked(task["id"]):
                save_log(task["id"], output, "BLOCKED", attempt)
                print(
                    f"[ORCHESTRATOR] {task['id']} ждёт ответа пользователя в docs/manual-queue.md. "
                    "Продолжаю со следующей задачей."
                )
                skipped.add(task["id"])
                break

            # Транзиентный сбой (сеть, провайдер): задача в roadmap не тронута — повторяем.
            save_log(task["id"], output, "NOT_DONE", attempt)
            if not success and attempt < max_attempts:
                print(
                    f"[ORCHESTRATOR] {task['id']}: попытка {attempt}/{max_attempts} не удалась, повторяю.",
                    flush=True,
                )
                continue

            print(
                f"[ORCHESTRATOR] {task['id']} не отмечена выполненной в roadmap. "
                "Останавливаюсь."
            )
            print("Проверь roadmap, manual-queue.md и лог сессии.")
            return 1

        if roadmap_done:
            completed += 1
            print(
                f"[ORCHESTRATOR] {task['id']} подтверждена через roadmap "
                f"({completed}/{limit or 'all'})."
            )

    print(f"Готово. Успешно завершено задач: {completed}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
