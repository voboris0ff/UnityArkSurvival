# AGENTS.md

## Project
- Unity **6000.0.85f1** (Unity 6)
- Render pipeline: **URP** (`com.unity.render-pipelines.universal` 17.0.4)
- Input: **Input System** (`com.unity.inputsystem` 1.20.0) — legacy Input Manager не использовать
- Key packages: AI Navigation 2.0.15, Timeline 1.8.13, uGUI 2.0.0, Visual Scripting 1.9.12, Test Framework 1.6.0, Multiplayer Center 1.0.0, Pipeline 0.9.1-exp.1

## Folder structure
```
Assets/
  Scripts/
    Core/        # загрузка, менеджеры, сервисы, bootstrap
    Player/      # контроллер игрока, ввод, здоровье, инвентарь
    AI/          # поведение NPC/животных (NavMesh, StateMachine)
    Systems/     # крафт, выживание, спавн, погода, время суток
    UI/          # HUD, меню, виджеты
    Data/        # ScriptableObject-описания, конфиги, enum'ы
    Utils/       # расширения, хелперы, утилиты
  Prefabs/
    Player/      # префабы игрока
    Enemies/     # префабы существ
    Environment/ # окружение, ресурсы, постройки
    UI/          # префабы интерфейса
  Art/           # модели, текстуры, материалы, анимации
  Audio/         # звуки и музыка
  Scenes/        # сцены
  Settings/      # URP-ассеты и настройки проекта
  Resources/     # только при реальной необходимости
```

## C# rules
- Публичные члены: `PascalCase`. Приватные поля: `_camelCase`.
- Вместо `public`-полей — `[SerializeField] private Type _field;`.
- Namespace соответствует папке, например `ArkSurvival.Player` для `Assets/Scripts/Player`.
- Один класс на файл, имя файла = имени класса.
- `Update`/`FixedUpdate` — только для логики, кэшируй ссылки (`Awake`/`Start`).
- Локальные методы и поля — `camelCase`.

## Forbidden
- Не открывать и не редактировать `.unity`, `.prefab`, `.meta`, `.asset`, `.mat`, `.anim`, `.controller`.
- Не создавать `.meta` вручную — Unity создаёт сам.
- Не использовать `GameObject.Find`, `FindObjectOfType`, `GetComponent` внутри `Update`.
- Не добавлять/обновлять пакеты без согласия.
- Не запускать git commit/push.

## Workflow
- Минимальные точечные правки; не трогать файлы вне задачи.
- Перед крупной правкой — сначала предложить план.
- Не запускать сборку и тесты: результат проверяет пользователь в редакторе Unity.
- При ошибке компиляции пользователь пришлёт строку из консоли Unity.