# DateBot

A .NET 10 userbot that answers likes in dating services on the owner's behalf and reports everything important through a notifier bot. First platform: Tashdate (its bot chat and Telegram Mini App); Badoo is next.

## Where to look

- **C#**: before writing or reviewing code, read [CODING_STANDARDS.md](CODING_STANDARDS.md) (layers, kind folders, `Result` errors, value objects, commands, adapters, persistence and migrations, tests).
- **Behaviour and settings**: [README.md](README.md) (how the service works, `appsettings.json` sections, notifier buttons, "Implementation decisions" with their reasons).
- **Tashdate Mini App**: [docs/tashdate-miniapp-api.md](docs/tashdate-miniapp-api.md) (`initData` sign-in, endpoints, error shapes).
- **New bot message format**: [docs/samples/README.md](docs/samples/README.md) (preparing a sample, where the parser test goes).
- **`*.ru.md`**: the owner's Russian translations. Read the English file; edit the `.ru.md` twin in the same change.

## Commands

```bash
dotnet tool restore
dotnet build DateBot.slnx
dotnet test DateBot.slnx
dotnet format DateBot.slnx --severity warn
dotnet format DateBot.slnx --verify-no-changes --severity warn
```

`dotnet format` without `--verify-no-changes` fixes whitespace, line endings and usings. `dotnet tool restore` installs `dotnet ef` for migrations.

## Done = green

The work is done when all of this is green:

1. `dotnet build` passes. Warnings, unused usings and broken `cref`s fail it.
2. `dotnet test` passes, and every new or changed behaviour has a test.
3. `dotnet format --verify-no-changes` reports nothing.
4. EF model changed: a migration exists and `has-pending-model-changes` reports no changes.
5. Something the owner sees changed (behaviour, setting, button, notification text): README and `appsettings.json` describe the new state.
6. `README.md` or `CODING_STANDARDS.md` changed: its `.ru.md` twin says the same.

Commit as a separate command after a green run.

## Guardrails

- **Live accounts are read-only.** In the real Telegram, the Tashdate bot chat and the Mini App, read freely. Like, message or press buttons there only when the owner asks directly: it is their account and their dates.
- **Secrets stay out of the repo.** `api_id`, `api_hash`, phone, tokens and keys live in user-secrets or environment variables. Session, database and photos live in `%LOCALAPPDATA%\DateBot`.
- **The reader picks the language.** Strings the owner reads in the running service (notification texts, `Error.Message`, settings validation messages) are Russian. Everything else is English: code, comments, logs, commits (imperative subject), docs.
