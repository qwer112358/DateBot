# TashDate: the bot and the Mini App

What is known about `@Znakomstva_Tashkentt_bot` as of 2026-10-17. The API was worked out from the Mini App's public code and from read-only GET requests made from the owner's account. No likes, messages or other changing requests were sent during the research, so their bodies come from the app's code and their responses are not verified live.

## Bot messages in the chat

| Event | Text | Button |
| --- | --- | --- |
| Match | Photo + «❤️ У вас мэтч с {имя}!\n\nВы оба лайкнули друг друга. Самое время написать.\n@{username}» | «💛 Открыть знакомства» (web app) |
| Match with a person without a username | The same without the `@{username}` line | «💛 Открыть знакомства» |
| Someone liked you | «Тобой интересуются\n\nЗагляни в приложение — вдруг это взаимно.» | «💛 Открыть знакомства» |
| Likes summary | «{Имя владельца}, тебя лайкнули N раза за последние 24 часа\n\nОткрой приложение — посмотри кто и ответь лайком» | «Посмотреть кто лайкнул» |

Like notifications carry no profile: who liked you is visible only in the Mini App. Names repeat (one chat had two different «Shaxnoza», one of them without a username), so a person without a username is identified by the name together with the card's photo, and in the app is looked up only among matches without a username.

## Mini App

- Address: `https://courteous-imagination-production-c2bc.up.railway.app/` (Railway hosting; the address may change, so take it from the web app button instead of hardcoding it).
- A single `index.html` page with inline JS; the API is on the same domain under `/api/`.
- Auth: every request carries the header `X-Telegram-Init-Data: <Telegram.WebApp.initData>`. The userbot gets `initData` itself through `messages.requestWebView` / `messages.requestMainWebView` to the bot (as when «Открыть знакомства» is pressed) and takes the `tgWebAppData` parameter after `#` in the returned URL.
- The internal user id (`ME.id`) differs from the Telegram id: `GET /api/user/{telegram_id}` → field `id`.

### Endpoints the service needs

| Request | Response / body |
| --- | --- |
| `GET /api/user/{telegram_id}` | Own profile: `id`, `name`, `age`, `city`, `bio`, `interests`, `profile_extras`, `photos`, `username`, `telegram_id`, ... |
| `GET /api/likes/{me}?limit={n}&offset={k}` | Array of incoming likes: `id`, `name`, `age`, `city`, `photos`, `is_verified`, `has_gift`, `locked`. Headers `X-Likes-Total`, `X-Likes-Offset-Next`, `X-Likes-Has-More` (`1`/`0`) |
| `GET /api/user/id/{id}` | Profile: `id`, `name`, `age`, `city`, `bio`, `interests`, `height`, `profile_extras`, `photos`, `communication_language`, `dating_goal`, `gender`, `is_verified`, `last_seen`. No `username` |
| `POST /api/like` `{from_user, to_user}` | `{match: bool, username?: string, ad?: ...}`; `429 {error: "like_limit_reached", limit, verified, verify_bonus}` (per the code: 30 a day, +20 after verification); `403` when the profile is restricted |
| `POST /api/likes/{me}/dismiss/{id}` | Hide an incoming like (✕ in the «Лайки» tab) |
| `GET /api/matches/{me}` | Matches: `id`, `name`, `age`, `photos`, `username`, `last_seen`, `match_created_at`, `is_verified` |
| `GET /api/chat/list?user_id={me}` | In-app chats |
| `GET /api/chat/messages?user_a={me}&user_b={id}[&after_id=…]` | Messages of an in-app chat |
| `POST /api/chat/send` `{from_user, to_user, text, client_key, reply_to_id}` | `{id}`; `client_key` is the client's idempotency key; `403 chat_unavailable` if the chat is closed |
| `GET /api/i18n/{ru\|uz}` | `{lang, strings}`: labels for codes (`interest.*`, `pets.*`, `education.*`, ...) |
| `GET /photos/{file}` | Profile photo |

### Field formats

- `photos`, `interests`: a JSON array of strings serialised into a string; interests are codes (`football`, `gym`, ...) with labels in `strings["interest.<code>"]`.
- `profile_extras`: a JSON object serialised into a string: `pets`, `sport`, `education`, `family_plans`, `comm_style` (codes) and `habits` (an object of flags); may be `null` or `{}`.
- `communication_language`: the profile's language code.
- Empty profiles are common: some likes have neither a bio nor interests.
- Errors: `{"detail": {"code": …}}` (`profile_*` means your profile is restricted, `chat_unavailable` means the chat is closed) or `{"error": …}` (`like_limit_reached`).

## How DateBot uses the API

| Step | Requests | Code |
| --- | --- | --- |
| Sign-in | `messages.requestWebView` to the bot (the menu button URL or «Открыть знакомства»), then `GET /api/user/{telegram_id}` and `GET /api/i18n/ru` | `WTelegramTashdateClient.OpenWebAppAsync`, `TashdateAppClient` |
| New likes | `GET /api/likes/{me}?limit=30&offset=…` (two pages at most) | `SyncAppLikesHandler` |
| Profile | `GET /api/user/id/{id}`, photo `GET /photos/{file}` | `ProcessAppLikeHandler`, `AppResponses.Profile` |
| Like back | `POST /api/like`; an interrupted like is first looked up in `GET /api/matches/{me}` | `LikeBack` |
| Profile for a match card | `GET /api/matches/{me}` by `@username`, then `GET /api/user/id/{id}` | `EnrichFromApp` |
| Message to the in-app chat | `POST /api/chat/send` with `client_key` = the DateBot profile id | `GenerateAndSendReply` |

How a failed request is treated:

- `401`, or `403` without a code: sign-in rejected; `initData` is requested again and the request is retried once.
- `429 like_limit_reached`: likes pause for an hour; the bot chat and messages keep working.
- `403/404 profile_*`: your profile is restricted; a 6-hour pause and a notification.
- `5xx` or a broken connection on `POST /api/chat/send`: outcome unknown, the message is never retried; other requests are retried later.
