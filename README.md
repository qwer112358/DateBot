# DateBot

A local .NET service that answers likes in dating services. It reads the profile and answers "you were liked" with a like back and a personal message, and a "mutual match" with a message only. A separate notifier bot tells you about everything important.

Russian version: [README.ru.md](README.ru.md).

The first supported platform is Tashdate. The service runs as a userbot under your Telegram account, reads the Tashdate bot's notifications and, on your behalf, opens its Mini App, where incoming likes are visible. Badoo is planned next. Reply generation, storage, limits and the notifier bot are not tied to Tashdate. Everything Tashdate-specific sits behind the ports `ITashdateClient` (chat with the bot) and `ITashdateApp` (Mini App), their adapters and the `Parsing` and `MiniAppClient` settings sections. A second platform needs its own adapters and a platform marker on the profile (`TashdateUserId` → a generic id).

Built from the spec «userbot для Tashdate (авто-ответ на лайки)». The bot's notification format was checked against real messages. The Mini App API was taken from its code and checked with read requests; the first real run will confirm the responses to a like and to an in-app chat message.

## Quick start

You need the .NET 10 SDK, `api_id` and `api_hash` from [my.telegram.org](https://my.telegram.org), a notifier bot token from BotFather and (optionally) an Anthropic API key.

1. Secrets go into user-secrets and never reach the repository:

```bash
cd src/DateBot.Worker
dotnet user-secrets set "Telegram:ApiId" "1234567"
dotnet user-secrets set "Telegram:ApiHash" "0123456789abcdef0123456789abcdef"
dotnet user-secrets set "Telegram:PhoneNumber" "+998901234567"
dotnet user-secrets set "Notifier:BotToken" "123456:ABC..."
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "Replies:Owner:Name" "Your name"
dotnet user-secrets set "Replies:Owner:About" "what you do"
```

2. Run it the first time in a console: Telegram sends a login code to type in (and asks for the two-step verification password if it is on). The session is then kept in `%LOCALAPPDATA%\DateBot\telegram.session`, outside the repository.

```bash
dotnet run --project src/DateBot.Worker
```

3. Send `/start` to the notifier bot. The service remembers the chat and takes commands only from the Telegram account the userbot is signed in as.

Without secrets the service refuses to start and lists what is missing (FluentValidation config check at start-up).

## Notifier bot commands

| Command or button | What it does |
| --- | --- |
| `/start` | Registers the chat for notifications; resumes work if the service was stopped |
| `/stop` | Emergency stop: no new likes or messages go out; the flag survives a restart |
| `/status` | Mode, cursor, actions in the last hour, counters of bot messages and profiles |
| `/likes` | Checks the «Лайки» tab in the app now: new likes come as cards, then a summary (open, hidden until premium, new cards, waiting for your answer) |
| «❤️ Ответить» / «⏭ Пропустить» | Decision on a like card: like back, then the message as usual (a draft in `Confirm`); or no like, and the like stays in the app for a manual answer |
| «✈️ В Telegram» / «💬 В приложении» / «⏭ Пропустить» | Decision on a draft in `Confirm` mode: where to send the message, or not to send it. Only channels that reach the person are shown: Telegram if the `@username` is known, the app if the profile id is known |
| «🔄 Найти в приложении» | On a draft with nowhere to send (the person has no `@username` and was not found in the app): searches again and, if found, sends a card with the «💬 В приложении» button |
| «✏️ Изменить» / «🤖 Переписать» | On a draft: your next message becomes the new text, or tells the model what to change («короче», «спроси про собаку»). The card comes again with the result. Your own text follows the same rules: no links, phone numbers or `@mentions` |
| `/cancel` | Leaves the draft you started editing as it was and brings its card back; any other command does the same |
| `/drafts` | Sends every draft that waits for your decision again: when cards got buried in the chat or came from an older version without the newer buttons |
| «🗑 Забыть» | Deletes everything about the person: profile, actions, the original notification, photo. Only a fingerprint of the id stays, so the person is never messaged again |

## How it works

1. At start the service signs in to Telegram, finds the chat with the Tashdate bot and reads the history from the cursor (`messages.getHistory` with `min_id`).
2. Every bot message is first stored as is (table `BotMessages` in the SQLite file `%LOCALAPPDATA%\DateBot\datebot.db`), then parsed by the rules from the config.
3. A new profile creates a `Match` aggregate; the like back and the message are written to the `Actions` journal as `Pending` before they run.
4. The message's final status and the cursor move are saved in one transaction. FLOOD_WAIT, the hourly limit, `/stop` and network failures leave the message unprocessed; it is retried later without repeating the steps already done.
5. After the bot chat the service opens the Mini App on your behalf (like the «Открыть знакомства» button does) and reads the «Лайки» tab. For each new like it loads the full profile and photo and, with `MiniApp:LikeBack = Confirm` (the shipped config), sends you a card with «❤️ Ответить» and «⏭ Пропустить». After «❤️ Ответить» it likes back with a pause and within the limit and, since a like back gives a match at once, prepares a message. With `Auto` it likes back without a card.
6. In `Confirm` mode (the shipped config) the draft with the profile and photo comes to the notifier bot, and you choose where to send it: Telegram DM or the in-app chat. If a channel refuses (say, the person closed DMs), the card comes again so you can pick another.
7. Then the service waits for new bot messages (live updates) and re-reads the chat every `PollInterval`; app likes are checked every `MiniApp:PollInterval`, right after the bot's «Тобой интересуются» hint, and on `/likes`.

Some match cards come without `@username` because the person has none. Telegram is then out, and the service looks for the person among the app's matches without `@username` with the same name. If there is exactly one, the card comes with the «💬 В приложении» button. If there is none, or there are several namesakes, the draft still comes, marked "nowhere to send", so you learn about the match and can write yourself. This works in `Auto` mode too.

One person can arrive both as an app like and as a match card in the bot chat. The service recognises them by any key (app profile id, `@username`) and does not message them twice. A match card for someone you liked yourself in the feed is enriched with the app profile: the bio and interests go into the prompt, and the in-app chat becomes an available channel.

The profile photo is downloaded as soon as the person appears (from the bot chat or the app) and kept locally in `%LOCALAPPDATA%\DateBot\photos`. It comes to the notifier bot with every message about that person: draft, "sent", "liked back", error. Separately the photo goes to Claude along with the profile text: the model may build on a neutral detail (a place, an activity, a pet) but does not discuss appearance. `Replies:UsePhoto = false` turns off only the Claude part; notifications keep the photo.

## Parsing configuration

The rules live in the `Parsing` section of `src/DateBot.Worker/appsettings.json`:

| Setting | Purpose |
| --- | --- |
| `LikeMarkers` | Text fragments of "you were liked" |
| `MutualMarkers` | Fragments of "mutual match"; checked first |
| `IgnoreMarkers` | Bot service messages that need no reaction |
| `LikeHintMarkers` | "You were liked" hints without a profile; they trigger an app likes check at once |
| `ProfilePatterns` | Profile regexes with groups `name` (required), `age`, `city`, `bio`; the first match wins |
| `InterestsPattern`, `UsernamePattern`, `PersonIdPattern` | Interests, `@username` from a mutual match, a stable profile id if the bot shows one |
| `UnknownMessages` | `ParseFailed` (default, with a notification to you) or `Ignore` |

If the bot sends something unrecognised, it goes to `ParseFailed`, and you get the original text and the list of buttons to adjust the markers and the pattern. Without `PersonIdPattern` a person is identified by the fingerprint "name + age + city + start of the bio", so a like and the later mutual match count as one person.

Buttons and the sending method are set in the `Processing` section:

| Setting | Default | Purpose |
| --- | --- | --- |
| `Mode` | `Confirm` | `Confirm`: the draft comes to the notifier bot, you pick the channel. `Auto`: sent at once to the preferred channel |
| `LikedMeMessage` | `AfterLikeBack` | `OnMutual`: scenario A only likes back; the message waits for the mutual match |
| `MutualRoute` | `DirectToUser` | Preferred channel in `Auto` and the button order: `DirectToUser` (Telegram), `AppChat` (in-app chat), `ViaBot` |
| `Channels` | `Telegram, App` | Allowed channels. The Tashdate bot does not forward text to profiles, so `BotChat` is off |
| `MiniApp:Enabled` | `true` | Answers to likes and the chat go through the Mini App; `false` = bot chat only |
| `MiniApp:LikeBack` | `Confirm` | `Confirm`: a new like comes as a card, the like back waits for «❤️ Ответить». `Auto`: liked back at once |
| `MiniApp:PollInterval` | 10 min | How often to check the «Лайки» tab |
| `MiniApp:MaxLikesPerPass` | 10 | How many new likes one check answers or sends as cards |
| `LikeBackButtons` | `❤, 👍, Лайк, Like, Yoqdi` | Like button captions (substring match) |
| `MessageButtons` | empty | "Write a message" button pressed before sending text to the bot |
| `MinPause` / `MaxPause` | 5 s / 30 s | Random pause before each action |
| `MaxActionsPerHour` | 20 | Limit of likes and messages per hour (counted from the journal, survives a restart) |
| `FirstRunBackfill` | 0 | On the very first run: how deep to read the old history. 0 = new notifications only |
| `RetentionDays` | 0 | Data retention, 0 = forever |
| `Filters` | off | Groundwork for age, city and stop-word filters |

The `MiniAppClient` section configures the Mini App connection: `Url` (empty = taken from the bot's button; it may change on Railway), `Language` (language of the labels for interests and profile fields), `Timeout`, `SessionLifetime` (how often to get a fresh Telegram-signed sign-in, `initData`), `UserAgent` (not sent by default).

Button captions are compared by whole word and in config order: `Лайк` never presses `Дизлайк`. If the bot's buttons are a reply keyboard (not inline), a press sends the button text. That is safe only for the bot's last message, so for older messages such a press is skipped with an error, to avoid liking another person. For the same reason, text without a "write" button (`MessageButtons` empty) goes to the bot only if its last message is that very profile.

## Message generation

The text is written by `claude-haiku-5-5` (set in `Anthropic:Model`) from the profile, the photo and your profile in `Replies:Owner`. The profile goes to the model as data inside `<profile>` tags with angle brackets escaped, and the system rules say to ignore any instructions in it. Before sending, code checks the text: length, language, no links, phone numbers, `@mentions`, emoji spam or traces of instructions. If the check fails or the API is down, a template from `Replies:Templates` goes out instead (or a draft in `Confirm`, if `InvalidReplyPolicy = Confirm`).

«🤖 Переписать» on a draft sends the model the same rules and profile plus the current draft in `<draft>` tags and your hint. The hint outranks the rules on length, language and tone; the other rules and checks stay. A failed rewrite keeps the old draft instead of a template, since you read the result before it goes anywhere. The prompts are built in `PromptBuilder` (`src/DateBot.Application/Replies/Services`).

## Stages and how to check them

| Stage | What to enable | Spec criterion |
| --- | --- | --- |
| 1. Connect and read | Run with `Processing:Mode = Confirm`; bot messages land in `BotMessages` | After a restart the missed messages are fetched, no duplicates |
| 2. Profile parsing | Collect 20–30 real notifications, anonymise them, add them to the parser tests (`docs/samples`) | No parse errors, or they go to `ParseFailed` |
| 3. Actions in `Confirm` | Check the like buttons and the message route on 20–30 drafts | 20–30 replies in a row without errors or exceeded limits |
| 4. `Auto` and operation | `Mode = Auto`, autostart | A week without lost notifications and without excess FLOOD_WAIT |

Autostart on Windows at logon (after the first interactive sign-in):

```bash
dotnet publish src/DateBot.Worker -c Release -o C:/apps/tashdatebot
schtasks /Create /TN "DateBot" /TR "C:\apps\tashdatebot\DateBot.Worker.exe" /SC ONLOGON /RL LIMITED
```

The session, database and photos live in `%LOCALAPPDATA%\DateBot` regardless of the publish folder; logs go to `logs/` next to the app. To keep data next to the app, set `Storage:DataDirectory` (e.g. `data`).

## Solution structure

| Project | Contents |
| --- | --- |
| `DateBot.Domain` | Aggregates `Match`, `SyncCursor`, `InboxMessage`; value objects with factories and FluentValidation validators; domain events; `Error` |
| `DateBot.Application` | Use cases (`ProcessBotMessage`, `LikeBack`, `GenerateAndSendReply`, `CatchUpHistory`, `SendReply`, owner commands), ports, parser, prompt, reply check, a light dispatcher with a validation decorator |
| `DateBot.Infrastructure` | WTelegramClient (userbot and Mini App sign-in), Mini App HTTP client, Telegram.Bot (notifier), Anthropic C# SDK, EF Core + SQLite with migrations, clock, pauses, photo store |
| `DateBot.Worker` | Composition root: DI, config, settings check at start-up, Serilog, background services |
| `tests/*` | Domain, use cases on fake ports, parser, architecture test (NetArchTest), storage on real SQLite |

File layout and coding rules: [CODING_STANDARDS.md](CODING_STANDARDS.md). Agent instructions and the definition of done: [AGENTS.md](AGENTS.md).

Tests:

```bash
dotnet test DateBot.slnx
```

## Implementation decisions

- **No EF value converters for `Maybe<T>`.** EF Core cannot store `Maybe<T>` in a nullable column through a value converter (checked with a prototype). So tables map to plain records, and `RecordMapper` restores the aggregate through the same domain factories. A broken DB row gives a `storage.corrupted` error, not a broken object, as the spec requires.
- **`BotMessages` table.** Added to the spec's three tables: stage 1 requires keeping raw messages, and `ParseFailed` messages must wait for manual review.
- **`Skipped` status.** Added for «Пропустить» in `Confirm` mode.
- **At most one message beats a retry.** If the service crashed or the connection broke mid-send, the message is not repeated (nobody knows whether it went out): the profile goes to `Failed` and you get a notification. Only Telegram refusals (FLOOD_WAIT and the like) and failures before sending are retried. An unfinished like is retried, as in the spec; if the like button is gone, the like counts as done.
- **Deleted data never opens the way to a second message.** After «Забыть» and the retention purge only the person's id (profile fingerprint) stays in `ForgottenPeople`, and the service ignores their new notifications.
- **Data outside the repository.** The session, DB and photos default to `%LOCALAPPDATA%\DateBot`, so a rebuild or `git clean` cannot remove them. The spec's "database next to the app" is one setting away: `Storage:DataDirectory`.
- **First run.** Without a cursor the service starts from now instead of reading the whole bot chat from the beginning (`FirstRunBackfill`).
- **Bot chat and app take turns.** One loop first reads the bot chat, then answers app likes, so a match card cannot overtake the like back that caused it. The app's daily like limit (30, 50 after verification) pauses only likes: matches and messages keep working, and you get one notification.
- **A like card only wakes the loop.** «❤️ Ответить» marks the like back as started and wakes the main loop, which performs it like any unfinished like back; `/likes` wakes it too. Every app action stays in that one loop, so your answer cannot race with a pass over the same person, and a yes survives a restart. A card is not an action: it does not count towards `MaxActionsPerHour`.
- **A retry to the in-app chat is recognisable.** Each message carries a `client_key` equal to the DateBot profile id, and the app drops a duplicate by it. The rule "unknown whether sent, so no retry" still applies here.
- **Mini App sign-in without a browser.** Telegram signs `initData` on a `messages.requestWebView` request from your account, just as when the button is pressed; no app password is needed or stored.
- **Own dispatcher instead of MediatR.** Each command runs in its own DI scope; input validation is a decorator.
- **FluentAssertions 7.x.** The last Apache-2.0 version; 8.x requires a commercial licence.

## The Tashdate bot: what is known

The bot is `@Znakomstva_Tashkentt_bot` (set in `Telegram:TashdateBotUsername`). On a mutual match it sends a photo with this caption:

```
❤️ У вас мэтч с {имя}!

Вы оба лайкнули друг друга. Самое время написать.
@{username}
```

and an inline «💛 Открыть знакомства» button that opens the Mini App. The default settings parse exactly this format: the marker «у вас мэтч», the pattern `мэтч с (?<name>…)!`, and the person is identified by `@username` (`PersonIdPattern`), because the card has no age, city or bio. The message goes to the DM by `@username` and is built from the name and the photo.

About likes the bot only drops hints («Тобой интересуются», «тебя лайкнули N раза») without a profile; these messages are configured as `LikeHintMarkers` and trigger an app likes check. Who liked you is visible only in the «Лайки» tab inside the Mini App, so scenario A (like back) goes through its API rather than chat buttons. Likes hidden behind premium get no answer: they have no id. What is known about this API is in [docs/tashdate-miniapp-api.md](docs/tashdate-miniapp-api.md).

## Open questions

- The responses of `POST /api/like` and `POST /api/chat/send` come from the Mini App code; the first real like and message will confirm them (the log will show "Signed in to the Mini App…").
- Whether the bot sends anything else to the chat (service messages, broadcasts); add those to `IgnoreMarkers` when they show up in `ParseFailed`.
- Tashdate's rules on automation, and the choice of account: automating a user account and using the app's internal API can get it banned, so better not use your main account.
