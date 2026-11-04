#!/usr/bin/env python3
"""Run roadmap tasks one at a time in fresh OpenCode CLI sessions.

Requires: Python 3.10+, OpenCode CLI (`opencode`), run from the Unity project root.

Selection rules (must match docs/roadmap.md):
  [ ] pending - candidate, [x] done - skipped, [!] waiting for the user - skipped.
The roadmap status written by the agent is the single source of truth.

Что изменено по сравнению с прошлой версией (экономия токенов):
  * промпт не просит перечитывать AGENTS.md (OpenCode грузит его сам);
  * оркестратор сам проверяет, есть ли ответы пользователя в manual-queue.md,
    и если нет - запрещает агенту читать файл зря;
  * агент не пишет лог сессии и не обязан печатать служебную строку TASK_RESULT -
    лог пишет оркестратор (по событиям OpenCode);
  * при повторной попытке агент предупреждён, что часть работы уже сделана;
  * progress.md агент правит точечно, а не перечитывает и переписывает целиком.

Во время работы печатаются короткие отчёты (читаю файл, меняю файл ...),
в конце - одна строка результата по задаче и итог по запуску.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROADMAP = ROOT / "docs" / "roadmap.md"
QUEUE = ROOT / "docs" / "manual-queue.md"
CONFIG = ROOT / "auto-dev.yaml"
LOG_DIR = ROOT / "docs" / "session-logs"
AGENT = "autodev"
DEFAULT_MODEL = ""  # единственный источник модели — auto-dev.yaml
REQUIRED_FILES = [
    "AGENTS.md",
    "docs/project-context.md",
    "docs/progress.md",
    "docs/roadmap.md",
    "docs/manual-queue.md",
]
STAGE_RE = re.compile(r"^##\s+(?P<title>.+)$")
TASK_RE = re.compile(r"^\s*-\s*\[(?P<status>[ x!])\]\s+.*?\*\*(?P<id>[A-Z]+-\d+)\*\*")
ANSWER_RE = re.compile(r"\*\*Ответ пользователя:\*\*(.*?)(?=\n###\s|\n##\s|\Z)", re.DOTALL)

READ_TOOLS = {"read"}
CHANGE_TOOLS = {"edit", "write", "patch", "multiedit"}
SEARCH_TOOLS = {"grep", "glob", "list", "ls", "find"}


# --------------------------------------------------------------------------- roadmap

def parse_tasks(text: str) -> list[dict[str, str]]:
    tasks, stage = [], ""
    for line in text.splitlines():
        stage_match = STAGE_RE.match(line)
        if stage_match:
            stage = stage_match.group("title").strip()
            continue
        match = TASK_RE.match(line)
        if match and match.group("status") == " ":
            tasks.append({"id": match.group("id"), "stage": stage, "line": line.strip()})
    return tasks


def task_status(task_id: str) -> str:
    for line in ROADMAP.read_text(encoding="utf-8").splitlines():
        match = TASK_RE.match(line)
        if match and match.group("id") == task_id:
            return match.group("status")
    return ""


def user_answers() -> list[tuple[str, str]]:
    """Заполненные блоки «Ответ пользователя»: [(заголовок пункта, текст ответа)]."""
    try:
        text = QUEUE.read_text(encoding="utf-8")
    except OSError:
        return []
    answers: list[tuple[str, str]] = []
    for chunk in re.split(r"^###\s+", text, flags=re.MULTILINE)[1:]:
        title = chunk.splitlines()[0].strip()
        match = ANSWER_RE.search(chunk)
        if not match:
            continue
        body = re.sub(r"<!--.*?-->", "", match.group(1), flags=re.DOTALL).strip()
        if body:
            answers.append((title, short(body, 500)))
    return answers


# --------------------------------------------------------------------------- prompt

def build_prompt(task: dict[str, str], answers: list[tuple[str, str]], attempt: int) -> str:
    if answers:
        listed = "\n\n".join(f"Пункт «{title}»:\n{body}" for title, body in answers)
        queue_rule = (
            "Ниже ответы пользователя из docs/manual-queue.md. Разбери их по правилам AGENTS.md "
            "и удали полностью пункты, на которые ответ достаточный.\n\n"
            f"{listed}\n"
        )
    else:
        queue_rule = ("Ответов пользователя в docs/manual-queue.md нет - не читай этот файл, "
                      "пока не понадобится добавить в него новый пункт.")

    retry_rule = ""
    if attempt > 1:
        retry_rule = ("\nЭто повторная попытка: предыдущая оборвалась, часть работы могла быть "
                      "уже сделана. Сначала проверь, что уже изменено в нужных файлах, "
                      "и продолжай, а не начинай с нуля.\n")

    return f"""Выполни ровно одну задачу roadmap {task['id']} (этап: {task['stage']}).

Строка задачи:
{task['line']}
{retry_rule}
AGENTS.md уже загружен. {queue_rule}
Читай только файлы, нужные задаче; docs/progress.md и docs/project-context.md - только если без них не обойтись.
Без аудита проекта, сборки, тестов, git, правки Unity-ассетов.

Завершение (обязательно, минимум действий):
1. В docs/roadmap.md поставь [x] этой задаче (или [!], если она заблокирована).
2. В docs/progress.md правь точечно только строки «Выполнено» и «Следующая задача».
3. Новые ручные шаги - пункт в docs/manual-queue.md с пустым блоком `Ответ пользователя:`.
Финальный ответ - одна короткая строка: что сделано.
"""


# --------------------------------------------------------------------------- settings / command

def load_settings() -> dict[str, object]:
    settings: dict[str, object] = {
        "tasks_per_run": 5, "timeout": 1200, "max_attempts": 2, "model": DEFAULT_MODEL,
    }
    try:
        text = CONFIG.read_text(encoding="utf-8")
    except OSError:
        return settings
    scalars = {
        "tasks_per_run": ("tasks_per_run", int),
        "timeout_minutes": ("timeout", lambda v: int(v) * 60),
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


def build_command(prompt_file: Path, model: str) -> list[str]:
    """Промпт передаётся файлом: многострочный текст в аргументах cmd.exe обрывается."""
    opencode = shutil.which("opencode.cmd") or shutil.which("opencode")
    if not opencode:
        return []
    prompt = f"Выполни инструкцию из приложенного файла: {rel(str(prompt_file))}"
    command = [opencode, "run", "--agent", AGENT, "--model", model, "--format", "json",
               "--file", rel(str(prompt_file)), prompt]
    if os.name == "nt" and opencode.lower().endswith((".cmd", ".bat")):
        command = ["cmd.exe", "/c"] + command
    return command


def kill_process(proc: subprocess.Popen) -> None:
    try:
        if os.name == "nt":
            subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                           capture_output=True, check=False)
        else:
            proc.kill()
    except Exception:
        pass


# --------------------------------------------------------------------------- live reporting

class Session:
    """Накопитель событий одной попытки: шаги, токены, файлы, ошибки."""

    def __init__(self) -> None:
        self.start = time.time()
        self.steps = 0
        self.tok_in = self.tok_out = self.tok_cache = 0
        self.read: list[str] = []
        self.changed: list[str] = []
        self.errors: list[str] = []
        self.raw_tail: list[str] = []
        self.seen_calls: set[str] = set()
        self.last_text = ""

    def stamp(self) -> str:
        sec = int(time.time() - self.start)
        return f"[{sec // 60:02d}:{sec % 60:02d}]"


def rel(path: str) -> str:
    try:
        return str(Path(path).resolve().relative_to(ROOT)).replace("\\", "/")
    except (ValueError, OSError):
        return path


def short(text: str, limit: int = 90) -> str:
    text = " ".join(str(text).split())
    return text if len(text) <= limit else text[: limit - 1] + "…"


def k(number: int) -> str:
    return f"{number / 1000:.1f}k" if number >= 1000 else str(number)


def say(session: Session, message: str) -> None:
    print(f"  {session.stamp()} {message}", flush=True)


def describe_tool(session: Session, part: dict) -> None:
    tool = str(part.get("tool", "")).lower()
    state = part.get("state") or {}
    args = state.get("input") or {}
    path = args.get("filePath") or args.get("path") or args.get("file") or ""
    status = state.get("status", "")

    if status == "error":
        error = short(state.get("error", "ошибка инструмента"), 80)
        session.errors.append(f"{tool}: {error}")
        say(session, f"✗ {tool} не удался: {error}")
        return

    if tool in READ_TOOLS and path:
        name = rel(path)
        if name not in session.read:
            session.read.append(name)
        say(session, f"читаю {name}")
    elif tool in CHANGE_TOOLS and path:
        name = rel(path)
        if name not in session.changed:
            session.changed.append(name)
        verb = "создаю/переписываю" if tool == "write" else "меняю"
        say(session, f"{verb} {name}")
    elif tool in SEARCH_TOOLS:
        target = args.get("pattern") or args.get("query") or path or "."
        say(session, f"ищу «{short(target, 50)}»")
    elif tool in {"todowrite", "todoread"}:
        return
    elif tool == "bash":
        say(session, f"shell: {short(args.get('command', ''), 60)}")
    else:
        say(session, f"{tool or 'инструмент'} {short(path, 60)}".strip())


def handle_event(session: Session, event: dict) -> None:
    kind = event.get("type", "")
    part = event.get("part") or {}

    if kind == "tool_use":
        call_id = str(part.get("callID") or part.get("id") or "")
        status = (part.get("state") or {}).get("status", "")
        if status in {"pending", "running"}:
            return
        if call_id and call_id in session.seen_calls:
            return
        session.seen_calls.add(call_id)
        describe_tool(session, part)

    elif kind == "step_start":
        session.steps += 1

    elif kind == "step_finish":
        tokens = part.get("tokens") or {}
        cache = tokens.get("cache") or {}
        session.tok_in += int(tokens.get("input", 0) or 0)
        session.tok_out += int(tokens.get("output", 0) or 0)
        session.tok_cache += int(cache.get("read", 0) or 0)

    elif kind == "text":
        text = part.get("text") or ""
        if text.strip():
            session.last_text = text.strip()

    elif kind == "error":
        error = event.get("error") or {}
        message = (error.get("data") or {}).get("message") or error.get("name") or str(error)
        session.errors.append(short(message, 120))
        say(session, f"✗ ошибка: {short(message, 100)}")


def run_task(task: dict[str, str], attempt: int, timeout: int, model: str,
             debug: bool) -> tuple[Session, str]:
    """Запускает агента и стримит отчёты. Возвращает (сессия, причина прерывания или '')."""
    session = Session()
    prompt = build_prompt(task, user_answers(), attempt)
    prompt_file = prepare_prompt_file(prompt)
    if prompt_file is None:
        session.errors.append("не удалось записать файл промпта")
        return session, "prompt_file"
    command = build_command(prompt_file, model)
    if not command:
        session.errors.append("OpenCode CLI не найден (добавь `opencode` в PATH)")
        return session, "no_cli"

    try:
        proc = subprocess.Popen(
            command, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            encoding="utf-8", errors="replace", bufsize=1,
        )
    except OSError as exc:
        session.errors.append(f"не удалось запустить OpenCode: {exc}")
        return session, "no_cli"

    timed_out = threading.Event()

    def on_timeout() -> None:
        timed_out.set()
        kill_process(proc)

    timer = threading.Timer(timeout, on_timeout)
    timer.start()
    try:
        assert proc.stdout is not None
        for raw in proc.stdout:
            line = raw.strip()
            if not line:
                continue
            if debug:
                print(f"  RAW {line[:300]}", flush=True)
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                session.raw_tail = (session.raw_tail + [line])[-5:]
                continue
            if isinstance(event, dict):
                handle_event(session, event)
        proc.wait()
    finally:
        timer.cancel()

    if timed_out.is_set():
        return session, "timeout"
    if proc.returncode not in (0, None):
        session.errors.append(f"OpenCode завершился с кодом {proc.returncode}")
        return session, "exit_code"
    return session, ""


# --------------------------------------------------------------------------- results / log

def summary_line(task_id: str, outcome: str, attempts: int, session: Session, reason: str) -> str:
    icon = {"DONE": "✔", "BLOCKED": "⏸", "NOT_DONE": "✗"}.get(outcome, "?")
    label = {"DONE": "выполнена", "BLOCKED": "ждёт ответа в manual-queue.md",
             "NOT_DONE": "НЕ выполнена"}.get(outcome, outcome)
    sec = int(time.time() - session.start)
    parts = [
        f"{icon} {task_id} {label}",
        f"попыток {attempts}",
        f"шагов {session.steps}",
        f"{sec // 60}м{sec % 60:02d}с",
        f"токены: вход {k(session.tok_in)}, кэш {k(session.tok_cache)}, выход {k(session.tok_out)}",
        f"файлов изменено {len(session.changed)}",
    ]
    if reason:
        parts.append(f"причина: {reason}")
    return " · ".join(parts)


def save_log(task_id: str, outcome: str, attempts: int, session: Session, reason: str) -> None:
    """Короткий лог (<= ~20 строк) из событий, без дампа вывода."""
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    now = dt.datetime.now()
    lines = [
        f"# Сессия {task_id}",
        f"- Время: {now.isoformat(timespec='seconds')}",
        f"- Результат: {outcome}" + (f" ({reason})" if reason else ""),
        f"- Попыток: {attempts}, шагов (последняя): {session.steps}",
        f"- Токены (последняя попытка): вход {k(session.tok_in)}, кэш {k(session.tok_cache)}, "
        f"выход {k(session.tok_out)}",
        f"- Прочитано: {', '.join(session.read[:8]) or '—'}",
        f"- Изменено: {', '.join(session.changed[:8]) or '—'}",
    ]
    if session.errors:
        lines.append(f"- Ошибки: {'; '.join(session.errors[-3:])}"[:300])
    if session.last_text:
        lines.append(f"- Ответ агента: {short(session.last_text, 200)}")
    path = LOG_DIR / f"{now.strftime('%Y%m%d-%H%M%S')}-{task_id}.md"
    try:
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    except OSError as exc:
        print(f"Не удалось записать лог сессии: {exc}", file=sys.stderr)


def archive_done_tasks() -> None:
    """Переносит строки [x] из docs/roadmap.md в docs/roadmap-archive.md, сохраняя заголовки этапов."""
    ARCHIVE = ROOT / "docs" / "roadmap-archive.md"
    try:
        roadmap_text = ROADMAP.read_text(encoding="utf-8")
    except OSError:
        return

    try:
        archive_text = ARCHIVE.read_text(encoding="utf-8")
    except OSError:
        archive_text = ""

    done_by_stage: dict[str, list[str]] = {}
    new_roadmap: list[str] = []
    current_stage = ""

    for line in roadmap_text.splitlines():
        stage_match = STAGE_RE.match(line)
        if stage_match:
            current_stage = line
            new_roadmap.append(line)
            continue

        task_match = TASK_RE.match(line)
        if task_match and task_match.group("status") == "x":
            done_by_stage.setdefault(current_stage, []).append(line.strip())
        else:
            new_roadmap.append(line)

    if not done_by_stage:
        return

    # Архив: идём по секциям старого файла, недостающие задачи вставляем в свою секцию.
    archive_lines = archive_text.splitlines()
    known = {line.strip() for line in archive_lines}
    result: list[str] = []
    section: str | None = None
    covered: set[str] = set()

    def flush_section() -> None:
        if section is not None:
            for task_line in done_by_stage.get(section, []):
                if task_line not in known:
                    result.append(task_line)
                    known.add(task_line)
            covered.add(section)

    for line in archive_lines:
        stage_match = STAGE_RE.match(line)
        if stage_match:
            flush_section()
            section = line
        result.append(line)
    flush_section()

    # Секции, которых в архиве ещё не было, — дописываем в конец.
    for stage, task_lines in done_by_stage.items():
        if stage in covered or not stage:
            continue
        result.extend(["", stage, ""])
        result.extend(task_lines)

    ARCHIVE.write_text("\n".join(result) + "\n", encoding="utf-8")
    ROADMAP.write_text("\n".join(new_roadmap) + "\n", encoding="utf-8")


def prepare_prompt_file(prompt: str) -> Path | None:
    """Пишет промпт в файл, чтобы не передавать многострочный текст аргументом cmd.exe."""
    try:
        temp = ROOT / "Temp"
        temp.mkdir(parents=True, exist_ok=True)
        path = temp / "auto-dev-prompt.md"
        path.write_text(prompt, encoding="utf-8")
        return path
    except OSError as exc:
        print(f"Не удалось записать файл промпта: {exc}", file=sys.stderr)
        return None


def main() -> int:
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # type: ignore[attr-defined]
    except Exception:
        pass

    parser = argparse.ArgumentParser(description="Run roadmap tasks in fresh OpenCode sessions.")
    parser.add_argument("--tasks", default=None, help="Количество задач или all.")
    parser.add_argument("--timeout", type=int, default=None, help="Таймаут одной сессии, секунд.")
    parser.add_argument("--model", default=None, help="Переопределить модель (provider/model#variant).")
    parser.add_argument("--dry-run", action="store_true", help="Показать команду и промпт, не запускать.")
    parser.add_argument("--debug-events", action="store_true",
                        help="Печатать сырые JSON-события OpenCode (для проверки формата).")
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
    if not model:
        print("Модель не задана: укажи `model:` в auto-dev.yaml или флаг --model.", file=sys.stderr)
        return 2
    max_attempts = max(1, int(settings["max_attempts"]))
    configured = int(settings["tasks_per_run"])

    if args.tasks == "all":
        limit = None
    else:
        try:
            limit = int(args.tasks) if args.tasks else configured
        except ValueError:
            parser.error("--tasks должен быть числом или all")
    if limit is not None and limit < 1:
        parser.error("--tasks должен быть положительным числом или all")

    if args.dry_run:
        tasks = parse_tasks(ROADMAP.read_text(encoding="utf-8"))
        if not tasks:
            print("Невыполненных задач не осталось.")
            return 0
        prompt = build_prompt(tasks[0], user_answers(), 1)
        prompt_file = prepare_prompt_file(prompt)
        command = build_command(prompt_file or ROOT / "Temp" / "auto-dev-prompt.md", model)
        print(json.dumps({"command": command, "timeout": timeout,
                          "max_attempts": max_attempts, "limit": limit},
                         ensure_ascii=False, indent=2))
        print("\n--- prompt ---\n" + prompt)
        return 0

    results: list[str] = []
    completed = 0
    skipped: set[str] = set()
    exit_code = 0

    while limit is None or completed < limit:
        tasks = parse_tasks(ROADMAP.read_text(encoding="utf-8"))
        task = next((t for t in tasks if t["id"] not in skipped), None)
        if task is None:
            print("Невыполненных задач не осталось.")
            break

        print(f"\n=== {task['id']} · {task['stage']} ===", flush=True)
        outcome, reason, session, attempt = "NOT_DONE", "", Session(), 0

        try:
            for attempt in range(1, max_attempts + 1):
                if attempt > 1:
                    print(f"  ↻ повторная попытка {attempt}/{max_attempts}", flush=True)
                session, reason = run_task(task, attempt, timeout, model, args.debug_events)

                status = task_status(task["id"])
                if status == "x":
                    outcome, reason = "DONE", ""
                    break
                if status == "!":
                    outcome, reason = "BLOCKED", ""
                    break
                if reason in {"no_cli", "prompt_file"}:
                    break
                if not reason:
                    reason = "агент завершился, но статус в roadmap не изменён"
                if attempt == max_attempts:
                    break
        except Exception as exc:  # один сбой не должен убивать прогон без лога
            outcome, reason = "NOT_DONE", f"исключение: {exc}"
            session.errors.append(str(exc))

        line = summary_line(task["id"], outcome, attempt, session, reason)
        print(line, flush=True)
        results.append(line)
        save_log(task["id"], outcome, attempt, session, reason)

        if outcome == "DONE":
            completed += 1
            try:
                archive_done_tasks()
            except OSError as exc:
                print(f"  не удалось обновить архив: {exc}")
        elif outcome == "BLOCKED":
            skipped.add(task["id"])
        else:
            if session.errors:
                print(f"  последняя ошибка: {session.errors[-1]}")
            print("Останавливаюсь. Проверь roadmap, manual-queue.md и docs/session-logs/.")
            exit_code = 1
            break

    print("\n=== Итог сессии ===")
    for line in results:
        print(line)
    print(f"Выполнено задач: {completed}.")
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
