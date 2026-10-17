# Tashdate notification samples

Anonymised bot messages for the parser tests (stage 2) go here.

Preparing a sample:

1. Take the original text from a `ParseFailed` notification of the notifier bot or from the `BotMessages` table (`%LOCALAPPDATA%\DateBot\datebot.db`).
2. Replace the name, city, bio, `@username` and any ids with made-up ones, keeping the line structure, punctuation, emoji and button captions.
3. Save it as `like-01.txt`, `mutual-01.txt` and so on; add the buttons as the last line in the form `[Кнопки: ❤️ | 👎 | 💌]`.

Then add the cases to `tests/DateBot.Application.Tests/Parsing/ParserTests.cs` (the bot's real format is covered by `TashdateFormatTests.cs` next to it) and, if needed, adjust the `Parsing` section in `appsettings.json`.
