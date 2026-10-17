# Стандарты кода DateBot

Так написан весь код решения, и так пишется новый. Ревью проверяет каждое правило ниже; некоторые из них проверяют ещё сборка или `ArchitectureTests`, это сказано у правила.

## Слои и зависимости

Clean Architecture, зависимости направлены внутрь. Правило проверяет `ArchitectureTests`.

| Проект | Может зависеть от | Что в нём |
| --- | --- | --- |
| `DateBot.Domain` | только CSharpFunctionalExtensions и FluentValidation | агрегаты, value objects, доменные события, `Error` |
| `DateBot.Application` | Domain, абстракции `Microsoft.Extensions.*` | use cases, порты, разбор сообщений, промпт и проверка ответа |
| `DateBot.Infrastructure` | Application | адаптеры портов: Telegram, Mini App, бот-уведомитель, Anthropic, EF Core |
| `DateBot.Worker` | всё | composition root: настройки, DI, фоновые сервисы |

Новая внешняя система подключается так: порт в `Application/Ports/<группа>`, адаптер в `Infrastructure/<Адаптер>`, регистрация в `Infrastructure/DependencyInjection.cs`. Группы портов: `Persistence` (репозитории, unit of work, хранилища состояния), `Dating` (площадка: `ITashdateClient`, `ITashdateApp`), `Services` (часы, уведомитель, LLM, фото, лимиты).

Площадка держится за портом. Всё, что знает о Tashdate, называется по Tashdate (`TashdateAppClient`, `TashdateUserId`); остальное называется нейтрально, чтобы подключить Badoo своими адаптерами.

## Файлы, папки, namespace

- Один тип на файл, имя файла совпадает с именем типа. Приватный вложенный тип может остаться внутри родителя.
- Внутри проекта папки по областям (`Matches`, `Parsing`, `MiniApp`, `Persistence`…), внутри области — папки по видам. Namespace повторяет путь к папке.
- Новый файл сразу кладётся в папку своего вида. Если вида в области ещё нет, создаётся папка с именем из таблицы.
- В корне области лежит только её ядро: агрегат и `<Область>Errors` (`Match`, `MatchAction`, `MatchErrors`), ошибки области Application (`AppErrors`, `TelegramErrors`, `OwnerErrors`), иерархия `OwnerNotification`.

| Слой | Папки внутри области |
| --- | --- |
| Domain | `ValueObjects`, `Enums`, `Events`, `States` (входы фабрик и снимки для восстановления), `Validators` |
| Application, `UseCases` | `Commands`, `Queries`, `Handlers`, `Validators`, `Models` (результаты), `Enums`, `OwnerRequests`, `Services` (общие шаги сценариев), `EventHandlers` |
| Application, остальные области | `Services`, `Models`, `Enums`, `Options`, `Validators` и свои иерархии (`Routes`, `Plans`, `BotNotifications`) |
| Application, общее | `Ports/{Persistence,Dating,Services}`, `Abstractions/{Messaging,Events,Errors}` |
| Infrastructure | по адаптерам (`Telegram`, `MiniApp`, `Notifier`, `Llm`, `Platform`, `Persistence`), внутри `Services` (реализации), `Models` (DTO), `Enums`, `Options`, `Validators`, `Abstractions` (внутренние швы между адаптерами, например `IWebAppLauncher`); в `Persistence` — `Records`, `Repositories`, `UnitOfWork`, `Mapping`, `Migrations` |
| Worker | `Workers` (фоновые сервисы), `Configuration` (проверка настроек при старте) |
| Тесты | `Fakes`, `Support` и папки по областям |

## Стиль C#

- Форматирование задаёт `.editorconfig`: LF, file-scoped namespace, `using` с `System` первым и без пустых строк. Неиспользуемый `using` и битый `cref` ломают сборку.
- Классы и записи `sealed`. Реализации (обработчики, адаптеры, репозитории) `internal`; контракты (команды, модели, порты, настройки) `public`.
- Зависимости приходят через primary constructor.
- Метод из одного выражения пишется через `=>`.
- Коллекции: в моделях и value objects `ImmutableArray<T>`, в сигнатурах `IReadOnlyList<T>` / `IReadOnlyCollection<T>`; литералы `[]` и `[.. items]`; статические таблицы — `FrozenDictionary`.
- Регулярные выражения — `[GeneratedRegex]` в `partial`-классе.
- Имена: приватные поля `_camelCase`, `static readonly` — PascalCase (`Validator`), асинхронные методы с суффиксом `Async`, токен отмены называется `ct` и стоит последним.
- Числа и даты в строку — через `CultureInfo.InvariantCulture`.
- Время берётся из `IClock` (`UtcNow`, `DelayAsync`), случайность — из `IJitter`, новые id — `Guid.CreateVersion7(clock.UtcNow)`. Так тесты управляют временем.
- Комментарий — `<summary>` в одну строку. Он объясняет то, чего не говорит имя: причину, инвариант, откуда пришли данные. У записей `<param>` пишется только для неочевидных параметров. Члены с говорящим именем остаются без комментария.

## Ошибки: Result вместо исключений

- Всё, что может не получиться, возвращает `Result<T, Error>` или `UnitResult<Error>`. Отсутствие значения — `Maybe<T>`. Nullable `T?` остаётся в записях EF, JSON-моделях и классах настроек.
- `Error` создаётся фабрикой по типу: `Validation`, `NotFound`, `Conflict`, `External`, `RateLimited` (с `RetryAfter`), `Unexpected`.
- Ошибки области лежат в статическом классе `<Область>Errors` в её корне; общие — в `Application/Abstractions/Errors` (`ServiceErrors`, `DeliveryErrors`). Постоянная ошибка — `static readonly` поле, ошибка с параметрами — статический метод.
- Код ошибки — `область.snake_case` (`match.already_messaged`, `miniapp.unavailable`). `Message` читает владелец в уведомлении.
- Повтор решает код. `ErrorClassifier.IsRetryable` повторяет `RateLimited`, `service.paused` и любой код, оканчивающийся на `.unavailable` (с точкой). Такой код получает только временный сбой до передачи текста: сообщение бота остаётся необработанным, и сервис возьмётся за него снова. Окончательный отказ называется иначе, например `miniapp.chat_unavailable`.
- At-most-once. Если связь оборвалась после передачи текста, адаптер возвращает `DeliveryErrors.OutcomeUnknown` (через `AppErrors.SendOutcomeUnknown` / `TelegramErrors.SendOutcomeUnknown`). Такое сообщение не повторяется ни при каких условиях.
- Исключения живут только на границе адаптера: там ловятся конкретные типы с фильтром `when` и превращаются в `Error` (`Db.TryAsync`, `TashdateAppClient`). `OperationCanceledException` от собственного `ct` пробрасывается дальше. Domain и Application сообщают об ошибке значением. `GetValueOrThrow()` вызывается после проверки `HasValue` или там, где значение гарантирует инвариант.
- Обработчик — railway-цепочка `Create → Bind → Map → Tap`; лог ошибки пишется в `TapError`.

## Domain

### Value object

```csharp
public sealed record Age
{
    public const int Min = 18;
    public const int Max = 99;
    private static readonly AgeValidator Validator = new();

    private Age(int value) => Value = value;

    public int Value { get; }

    public static Result<Age, Error> Create(int value) =>
        Validator.ValidateAndCreate(value, "age.invalid", v => new Age(v));
}
```

- `sealed record` с приватным конструктором; объект создаётся только через `Create`, который валидирует вход через `ValidateAndCreate`.
- Валидатор лежит в `Validators/`, у каждого правила `WithErrorCode("<имя>.<правило>")`. Границы — константы самого value object (`Age.Min`), валидатор ссылается на них.
- FluentValidation превращается в `Error` в одном месте — `ValidationExtensions`.

### Агрегат

- `sealed class X : AggregateRoot<XId>` с приватным конструктором.
- `Create(XInput)` проверяет вход и поднимает событие создания. `Restore(XState)` восстанавливает агрегат из хранилища без событий; несогласованные данные дают ошибку `<область>.corrupted`. `XInput` и `XState` — записи в `States/`.
- Поведение возвращает `UnitResult<Error>`, инварианты проверяет `Guard.Require(условие, XErrors.Y)`, события поднимаются через `Raise`.
- Переходы статусов описаны таблицей разрешённых переходов (`Match.AllowedTransitions`).

### Доменные события

- `sealed record` в прошедшем времени (`LikeReceived`, `MessageSent`) с последним полем `DateTimeOffset OccurredAt`, в папке `Events/`.
- Unit of work рассылает события после коммита в `IDomainEventHandler` (`NotifyOwner`).

## Application

### Команды и запросы

| Что | Где | Вид |
| --- | --- | --- |
| Команда | `UseCases/Commands` | `public sealed record XCommand(Guid MatchId) : ICommand<TResult>` |
| Запрос | `UseCases/Queries` | `XQuery`, тот же `ICommand<TResult>` |
| Валидатор | `UseCases/Validators` | `XCommandValidator : AbstractValidator<XCommand>` |
| Обработчик | `UseCases/Handlers` | `internal sealed class XHandler : ICommandHandler<XCommand, TResult>` |
| Результат | `UseCases/Models`, `UseCases/Enums` | запись или перечисление исхода |

- Поля команды — примитивы; обработчик превращает их в value objects.
- Регистрация: `services.AddCommandHandler<XCommand, TResult, XHandler>()` в `Application/DependencyInjection.cs`. Валидатор подключится сам и выполнится декоратором; ошибка валидации вернётся как `Validation`-ошибка `XCommand.invalid`.
- Каждая команда выполняется через `ICommandDispatcher.SendAsync` в своём DI-scope со своим `DbContext`.
- Шаг, общий для нескольких обработчиков, — scoped-сервис в `UseCases/Services`, регистрируется там же.

### Порты

- Сигнатуры порта говорят доменными типами и `Result`. HTTP, TL-объекты и JSON остаются в адаптере.
- Репозиторий: `FindXAsync` возвращает `Result<Maybe<T>, Error>`, `AddAsync` и `RemoveAsync` только отмечают изменения. Сохраняет всё одним коммитом `IUnitOfWork.CommitAsync`.

### Уведомления владельцу

- Новое уведомление — наследник `OwnerNotification` в `Application/Notifications`. Текст и кнопки собирает `NotificationFormatter` (`Infrastructure/Notifier/Services`), нажатия разбирает `OwnerBotPoller.ParseCallback`.
- Уведомление о человеке всегда несёт его фото (`profile.Photo`).

## Infrastructure

- У каждой внешней системы своя папка-адаптер. Реализация — `internal sealed`, регистрация — в `Infrastructure/DependencyInjection.cs`.
- Ответы внешних API читаются в DTO из `Models/`, в доменные типы их переводит адаптер.

### Настройки

```csharp
public sealed class MiniAppClientOptions
{
    public const string Section = "MiniAppClient";

    /// <summary>How long one Telegram sign-in (init data) is used before a fresh one is requested.</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromMinutes(30);
}
```

- Класс `XOptions` с константой `Section` и `init`-свойствами с рабочими значениями по умолчанию; лежит в `Options/` своей области.
- `XOptionsValidator` лежит в `Validators/` рядом. Сообщение называет ключ настройки: `"MiniAppClient:Url — пусто или https-адрес Mini App"`.
- Регистрация в `src/DateBot.Worker/Program.cs`: `builder.Services.AddValidatedOptions<XOptions>(builder.Configuration, XOptions.Section)`. С неверной настройкой сервис не стартует и перечисляет ошибки.
- Новая настройка появляется в `appsettings.json` со значением по умолчанию и в таблице README.

### Хранение

- EF Core + SQLite. Таблицы маппятся на изменяемые записи в `Persistence/Records`. `RecordMapper` собирает из них агрегат через доменный `Restore`, поэтому битая строка даёт `storage.corrupted`, а не битый объект. `Maybe<T>` хранится через запись с nullable-полем: value converter EF для `Maybe<T>` в nullable-колонку не работает.
- Репозитории отмечают агрегаты в `ChangeSet`. `EfUnitOfWork` сохраняет их одной транзакцией и после этого рассылает доменные события.
- Исключения базы превращаются в `Error` через `Db.TryAsync`.
- Tombstones: при удалении человека (`RemoveAsync`, очистка по сроку) в `ForgottenPeople` остаются все его `IdentityKeys`; на эти ключи сервис больше не отвечает.
- Изменение схемы:
  1. Поменять запись и конфигурацию `DateBotDbContext`.
  2. `dotnet ef migrations add <Name> --project src/DateBot.Infrastructure --startup-project src/DateBot.Worker --output-dir Persistence/Migrations`.
  3. Обновление данных (`migrationBuilder.Sql`) — отдельной миграцией после миграции схемы: SQLite пересобирает таблицу, и EF предупреждает о SQL, который выполняется до пересборки.
  4. `dotnet ef migrations has-pending-model-changes --project src/DateBot.Infrastructure --startup-project src/DateBot.Worker` должна ответить, что изменений нет.
- Миграции — сгенерированный код. Руками в них дописывается только SQL обновления данных и `<summary>`.

## Worker

- `Workers/` — `BackgroundService`-циклы, которые вызывают команды через `ICommandDispatcher`. Ошибка команды пишется в лог, и цикл ждёт: `RetryAfter` для `RateLimited`, иначе растущую паузу `Backoff`. Сервис продолжает работу.
- Порядок в `Program.cs`: настройки → `AddApplication` → `AddInfrastructure` → фоновые сервисы → `DatabaseInitializer` перед `RunAsync`.

## Логи

- `ILogger<T>` с шаблоном и именованными параметрами: `logger.LogWarning("App like of {Name} failed: {Error}", name, error)`.
- Уровни: `Debug` — ожидаемый пропуск; `Information` — вехи работы (вход, итог прохода); `Warning` — не удалась одна операция, проход идёт дальше; `Error` — сорвался целый проход или вход, цикл уходит на повтор.
- В лог пишутся id, имена и коды ошибок. Секреты, `initData`, токены и ключи API в лог не попадают.

## Тесты

- xUnit + FluentAssertions 7 (8.x требует коммерческую лицензию) + NetArchTest.
- Проекты повторяют слои:
  - `Domain.Tests` — чистый домен;
  - `Application.Tests` — сценарии через `TestHost`: настоящие обработчики, DI и валидаторы, порты — фейки из `Fakes/`;
  - `Infrastructure.Tests` — адаптеры: настоящий файл SQLite с настоящими миграциями, HTTP через `StubHttpHandler`, привязка и проверка настроек.
- Класс `public sealed class XTests`. Имя теста — утверждение о поведении в snake_case: `Two_namesakes_without_username_are_not_guessed_between`.
- Тест сценария проверяет наблюдаемый исход: что лежит в хранилище, что получил уведомитель, какие вызовы ушли в фейк площадки. Неочевидную причину ожидания объясняет аргумент `because`.
- Сообщения бота берутся из `Support/Samples`; они построены по реальным анонимизированным сообщениям (`docs/samples`).
- Исправление бага начинается с теста, который красный без исправления.
- Фейк на каждый порт лежит в `Fakes/`, один тип на файл.
