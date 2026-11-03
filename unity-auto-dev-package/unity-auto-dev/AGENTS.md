# AGENTS.md

## Project
- Unity 6000.0.85f1 (Unity 6).
- Render pipeline: URP 17.0.4.
- Input: Input System 1.20.0. Не использовать Legacy Input Manager.
- AI Navigation: 2.0.15.
- Основной язык игровых скриптов: C#.
- Пространство имён соответствует папке: `ArkSurvival.AI` для `Assets/Scripts/AI`.

## Folder structure
```text
Assets/
  Scripts/
    Core/
    Player/
    AI/
      Creatures/
    Systems/
    UI/
    Data/
    Utils/
  Prefabs/
    Player/
    Creatures/
    Environment/
    UI/
  Art/
  Audio/
  Scenes/
  Settings/
  Resources/
docs/
scripts/
```

## C# rules
- Публичные члены: `PascalCase`; приватные поля: `_camelCase`.
- Вместо публичных полей использовать `[SerializeField] private`.
- Один класс на файл, имя файла совпадает с именем класса.
- Кэшировать ссылки в `Awake`/`Start`.
- Не использовать `GameObject.Find`, `FindObjectOfType` или `GetComponent` внутри `Update`.
- Не добавлять логику в `Update`, если её можно разместить в отдельном компоненте или тике системы.
- Избегать лишних абстракций и обобщённых фреймворков для одной сущности.
- Не вводить новые зависимости без необходимости.

## Forbidden
- Не открывать и не редактировать `.unity`, `.prefab`, `.meta`, `.asset`, `.mat`, `.anim`, `.controller`.
- Не создавать `.meta` вручную: Unity создаёт их сама.
- Не добавлять/обновлять пакеты без согласия пользователя.
- Не запускать Git commit/push.
- Не запускать Unity build, PlayMode/EditMode tests или длительные проверки без отдельного разрешения пользователя.

## Workflow
- Одна задача roadmap за одну сессию.
- Минимальные точечные изменения; не трогать файлы вне задачи.
- Не выполнять соседние задачи «заодно».
- Перед крупной правкой сначала предложить краткий план и дождаться подтверждения.
- Не запускать сборку и тесты. Пользователь проверяет результат в Unity Editor.
- Не утверждать, что код проверен компиляцией или в редакторе, если этого не происходило.
- Если требуется ручная настройка Unity Editor, добавить конкретный пункт в `docs/manual-queue.md` и продолжить только если оставшаяся часть задачи независима.
- При ошибке компиляции пользователь передаст сообщение из Console.
- После задачи обновить `docs/roadmap.md`, `docs/progress.md`, `docs/manual-queue.md` и создать краткий лог в `docs/session-logs/`.

## Context files
- `docs/project-context.md` — стабильная архитектура и технические решения.
- `docs/roadmap.md` — задачи и их статусы.
- `docs/progress.md` — актуальное состояние проекта.
- `docs/manual-queue.md` — действия, которые должен выполнить пользователь в Unity Editor.
