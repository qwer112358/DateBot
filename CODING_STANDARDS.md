# DateBot coding standards

All code in the solution is written this way, and new code follows suit. Review checks every rule below; the build or `ArchitectureTests` also enforce some of them, as noted at the rule.

## Layers and dependencies

Clean Architecture, dependencies point inward. `ArchitectureTests` enforces the rule.

| Project | May depend on | Contents |
| --- | --- | --- |
| `DateBot.Domain` | only CSharpFunctionalExtensions and FluentValidation | aggregates, value objects, domain events, `Error` |
| `DateBot.Application` | Domain, `Microsoft.Extensions.*` abstractions | use cases, ports, message parsing, prompt and reply check |
| `DateBot.Infrastructure` | Application | port adapters: Telegram, Mini App, notifier bot, Anthropic, EF Core |
| `DateBot.Worker` | everything | composition root: settings, DI, background services |

A new external system plugs in as: a port in `Application/Ports/<group>`, an adapter in `Infrastructure/<Adapter>`, registration in `Infrastructure/DependencyInjection.cs`. Port groups: `Persistence` (repositories, unit of work, state stores), `Dating` (the platform: `ITashdateClient`, `ITashdateApp`), `Services` (clock, notifier, LLM, photos, limits).

The platform stays behind a port. Whatever knows about Tashdate is named after Tashdate (`TashdateAppClient`, `TashdateUserId`); everything else gets a neutral name, so Badoo can plug in with its own adapters.

## Files, folders, namespaces

- One type per file, the file named after the type. A private nested type may stay inside its parent.
- Inside a project, folders by area (`Matches`, `Parsing`, `MiniApp`, `Persistence`…); inside an area, folders by kind. The namespace mirrors the folder path.
- A new file goes straight into the folder of its kind. If the area has no such kind yet, create the folder with the name from the table.
- An area's root holds only its core: the aggregate and `<Area>Errors` (`Match`, `MatchAction`, `MatchErrors`), the Application area errors (`AppErrors`, `TelegramErrors`, `OwnerErrors`), the `OwnerNotification` hierarchy.

| Layer | Folders inside an area |
| --- | --- |
| Domain | `ValueObjects`, `Enums`, `Events`, `States` (factory inputs and snapshots for restore), `Validators` |
| Application, `UseCases` | `Commands`, `Queries`, `Handlers`, `Validators`, `Models` (results), `Enums`, `OwnerRequests`, `Services` (shared scenario steps), `EventHandlers` |
| Application, other areas | `Services`, `Models`, `Enums`, `Options`, `Validators` and their own hierarchies (`Routes`, `Plans`, `BotNotifications`) |
| Application, shared | `Ports/{Persistence,Dating,Services}`, `Abstractions/{Messaging,Events,Errors}` |
| Infrastructure | per adapter (`Telegram`, `MiniApp`, `Notifier`, `Llm`, `Platform`, `Persistence`), inside: `Services` (implementations), `Models` (DTOs), `Enums`, `Options`, `Validators`, `Abstractions` (internal seams between adapters, e.g. `IWebAppLauncher`); `Persistence` has `Records`, `Repositories`, `UnitOfWork`, `Mapping`, `Migrations` |
| Worker | `Workers` (background services), `Configuration` (settings check at start-up) |
| Tests | `Fakes`, `Support` and per-area folders |

## C# style

- `.editorconfig` sets the formatting: LF, file-scoped namespaces, `using`s with `System` first and no blank lines. An unused `using` or a broken `cref` fails the build.
- Classes and records are `sealed`. Implementations (handlers, adapters, repositories) are `internal`; contracts (commands, models, ports, options) are `public`.
- Dependencies arrive through a primary constructor.
- A single-expression member uses `=>`.
- Collections: `ImmutableArray<T>` in models and value objects, `IReadOnlyList<T>` / `IReadOnlyCollection<T>` in signatures; literals `[]` and `[.. items]`; static lookup tables as `FrozenDictionary`.
- Regular expressions: `[GeneratedRegex]` in a `partial` class.
- Names: private fields `_camelCase`, `static readonly` in PascalCase (`Validator`), async methods end in `Async`, the cancellation token is named `ct` and comes last.
- Numbers and dates turn into strings with `CultureInfo.InvariantCulture`.
- Time comes from `IClock` (`UtcNow`, `DelayAsync`), randomness from `IJitter`, new ids from `Guid.CreateVersion7(clock.UtcNow)`. This lets tests control time.
- A comment is a one-line `<summary>` explaining what the name does not: the reason, the invariant, where the data came from. Records get `<param>` only for non-obvious parameters. Members with a telling name stay uncommented.

## Errors: Result instead of exceptions

- Anything that can fail returns `Result<T, Error>` or `UnitResult<Error>`. A missing value is `Maybe<T>`. Nullable `T?` stays in EF records, JSON models and options classes.
- An `Error` is made by the factory of its type: `Validation`, `NotFound`, `Conflict`, `External`, `RateLimited` (with `RetryAfter`), `Unexpected`.
- An area's errors live in a static `<Area>Errors` class at the area root; shared ones in `Application/Abstractions/Errors` (`ServiceErrors`, `DeliveryErrors`). A fixed error is a `static readonly` field, an error with parameters a static method.
- Error code: `area.snake_case` (`match.already_messaged`, `miniapp.unavailable`). The owner reads `Message` in a notification.
- The code decides the retry. `ErrorClassifier.IsRetryable` retries `RateLimited`, `service.paused` and any code ending in `.unavailable` (with the dot). Give such a code only to a transient failure before the text was handed over: the bot message stays unprocessed and the service takes it up again. A final refusal gets another name, e.g. `miniapp.chat_unavailable`.
- At-most-once. If the connection broke after the text was handed over, the adapter returns `DeliveryErrors.OutcomeUnknown` (via `AppErrors.SendOutcomeUnknown` / `TelegramErrors.SendOutcomeUnknown`). Such a message is never retried.
- Exceptions live only at the adapter boundary: catch specific types with a `when` filter and turn them into an `Error` (`Db.TryAsync`, `TashdateAppClient`). `OperationCanceledException` from our own `ct` propagates. Domain and Application report failures as values. `GetValueOrThrow()` is called after a `HasValue` check or where an invariant guarantees the value.
- A handler is a railway chain `Create → Bind → Map → Tap`; the error is logged in `TapError`.

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

- A `sealed record` with a private constructor; the only way in is `Create`, which validates the input through `ValidateAndCreate`.
- The validator lives in `Validators/`, every rule has `WithErrorCode("<name>.<rule>")`. Limits are constants of the value object itself (`Age.Min`), and the validator refers to them.
- FluentValidation results become an `Error` in one place: `ValidationExtensions`.

### Aggregate

- `sealed class X : AggregateRoot<XId>` with a private constructor.
- `Create(XInput)` validates the input and raises the creation event. `Restore(XState)` rebuilds the aggregate from storage without events; inconsistent data gives an `<area>.corrupted` error. `XInput` and `XState` are records in `States/`.
- Behaviour returns `UnitResult<Error>`, checks invariants with `Guard.Require(condition, XErrors.Y)` and raises events through `Raise`.
- Status changes follow a table of allowed transitions (`Match.AllowedTransitions`).

### Domain events

- A past-tense `sealed record` (`LikeReceived`, `MessageSent`) whose last field is `DateTimeOffset OccurredAt`, in `Events/`.
- The unit of work dispatches events after the commit to `IDomainEventHandler` (`NotifyOwner`).

## Application

### Commands and queries

| What | Where | Shape |
| --- | --- | --- |
| Command | `UseCases/Commands` | `public sealed record XCommand(Guid MatchId) : ICommand<TResult>` |
| Query | `UseCases/Queries` | `XQuery`, the same `ICommand<TResult>` |
| Validator | `UseCases/Validators` | `XCommandValidator : AbstractValidator<XCommand>` |
| Handler | `UseCases/Handlers` | `internal sealed class XHandler : ICommandHandler<XCommand, TResult>` |
| Result | `UseCases/Models`, `UseCases/Enums` | a record or an outcome enum |

- Command fields are primitives; the handler turns them into value objects.
- Registration: `services.AddCommandHandler<XCommand, TResult, XHandler>()` in `Application/DependencyInjection.cs`. The validator is picked up automatically and runs as a decorator; a failure comes back as a `Validation` error `XCommand.invalid`.
- Every command runs through `ICommandDispatcher.SendAsync` in its own DI scope with its own `DbContext`.
- A step shared by several handlers is a scoped service in `UseCases/Services`, registered in the same file.

### Ports

- Port signatures speak domain types and `Result`. HTTP, TL objects and JSON stay in the adapter.
- Repository: `FindXAsync` returns `Result<Maybe<T>, Error>`; `AddAsync` and `RemoveAsync` only mark changes. `IUnitOfWork.CommitAsync` saves everything in one commit.

### Owner notifications

- A new notification is an `OwnerNotification` subtype in `Application/Notifications`. `NotificationFormatter` (`Infrastructure/Notifier/Services`) builds the text and buttons, `OwnerBotPoller.ParseCallback` parses button presses.
- A notification about a person always carries their photo (`profile.Photo`).

## Infrastructure

- Every external system has its own adapter folder. The implementation is `internal sealed` and registered in `Infrastructure/DependencyInjection.cs`.
- External API responses are read into DTOs from `Models/`; the adapter maps them to domain types.

### Options

```csharp
public sealed class MiniAppClientOptions
{
    public const string Section = "MiniAppClient";

    /// <summary>How long one Telegram sign-in (init data) is used before a fresh one is requested.</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromMinutes(30);
}
```

- An `XOptions` class with a `Section` constant and `init` properties with working defaults, in its area's `Options/`.
- `XOptionsValidator` sits next to it in `Validators/`. The message names the setting key: `"MiniAppClient:Url — пусто или https-адрес Mini App"`.
- Registration in `src/DateBot.Worker/Program.cs`: `builder.Services.AddValidatedOptions<XOptions>(builder.Configuration, XOptions.Section)`. With an invalid setting the service refuses to start and lists the errors.
- A new setting appears in `appsettings.json` with its default and in the README table (`README.md` and `README.ru.md`).

### Persistence

- EF Core + SQLite. Tables map to mutable records in `Persistence/Records`. `RecordMapper` builds the aggregate from them through the domain `Restore`, so a broken row gives `storage.corrupted`, not a broken object. `Maybe<T>` is stored through a record with a nullable field: an EF value converter from `Maybe<T>` to a nullable column does not work.
- Repositories track aggregates in `ChangeSet`. `EfUnitOfWork` saves them in one transaction and then dispatches the domain events.
- Database exceptions become an `Error` through `Db.TryAsync`.
- Tombstones: when a person is deleted (`RemoveAsync`, retention purge), all their `IdentityKeys` stay in `ForgottenPeople`; the service never answers those keys again.
- Schema change:
  1. Change the record and the `DateBotDbContext` configuration.
  2. `dotnet ef migrations add <Name> --project src/DateBot.Infrastructure --startup-project src/DateBot.Worker --output-dir Persistence/Migrations`.
  3. A data update (`migrationBuilder.Sql`) goes into its own migration after the schema migration: SQLite rebuilds the table, and EF warns about SQL that runs before the rebuild.
  4. `dotnet ef migrations has-pending-model-changes --project src/DateBot.Infrastructure --startup-project src/DateBot.Worker` must report no changes.
- Migrations are generated code. Hand edits are limited to the data-update SQL and the `<summary>`.

## Worker

- `Workers/` holds `BackgroundService` loops that call commands through `ICommandDispatcher`. A command error is logged and the loop waits: `RetryAfter` for `RateLimited`, otherwise a growing `Backoff` pause. The service keeps running.
- Order in `Program.cs`: options → `AddApplication` → `AddInfrastructure` → background services → `DatabaseInitializer` before `RunAsync`.

## Logging

- `ILogger<T>` with a message template and named placeholders: `logger.LogWarning("App like of {Name} failed: {Error}", name, error)`.
- Levels: `Debug` for an expected skip; `Information` for milestones (sign-in, pass summary); `Warning` when one operation failed and the pass goes on; `Error` when a whole pass or sign-in failed and the loop goes into retry.
- Logs carry ids, names and error codes. Secrets, `initData`, tokens and API keys stay out of the log.

## Tests

- xUnit + FluentAssertions 7 (8.x needs a commercial licence) + NetArchTest.
- Projects mirror the layers:
  - `Domain.Tests`: pure domain;
  - `Application.Tests`: scenarios through `TestHost` with real handlers, DI and validators, and fakes from `Fakes/` for the ports;
  - `Infrastructure.Tests`: adapters on a real SQLite file with the real migrations, HTTP through `StubHttpHandler`, settings binding and validation.
- Class `public sealed class XTests`. A test name is a statement about behaviour in snake_case: `Two_namesakes_without_username_are_not_guessed_between`.
- A scenario test checks the observable outcome: what is in the store, what the notifier received, which calls reached the platform fake. The `because` argument explains a non-obvious expectation.
- Bot messages come from `Support/Samples`, built from real anonymised messages (`docs/samples`).
- A bug fix starts with a test that is red without the fix.
- Each port has a fake in `Fakes/`, one type per file.
