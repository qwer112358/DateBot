# TashDate: бот и Mini App

Что известно о `@Znakomstva_Tashkentt_bot` на 2026-10-17. API выяснен по публичному коду Mini App и по GET-запросам на чтение от аккаунта владельца. Лайки, сообщения и другие изменяющие запросы при исследовании не отправлялись, поэтому их тела взяты из кода приложения, а ответы на них не проверены вживую.

## Сообщения бота в чате

| Событие | Текст | Кнопка |
| --- | --- | --- |
| Мэтч | Фото + «❤️ У вас мэтч с {имя}!\n\nВы оба лайкнули друг друга. Самое время написать.\n@{username}» | «💛 Открыть знакомства» (web app) |
| Мэтч с человеком без username | То же, но без строки `@{username}` | «💛 Открыть знакомства» |
| Кто-то лайкнул | «Тобой интересуются\n\nЗагляни в приложение — вдруг это взаимно.» | «💛 Открыть знакомства» |
| Сводка лайков | «{Имя владельца}, тебя лайкнули N раза за последние 24 часа\n\nОткрой приложение — посмотри кто и ответь лайком» | «Посмотреть кто лайкнул» |

Уведомления о лайках анкету не содержат: кто лайкнул, видно только в Mini App. Имена повторяются (в одном чате встретились две разные «Shaxnoza», одна без username), поэтому человек без username опознаётся по имени вместе с фото карточки, а в приложении ищется только среди мэтчей без username.

## Mini App

- Адрес: `https://courteous-imagination-production-c2bc.up.railway.app/` (хостинг Railway; адрес может смениться, его стоит брать из кнопки web app, а не хардкодить).
- Одна страница `index.html` со встроенным JS, API на том же домене под `/api/`.
- Авторизация: каждый запрос несёт заголовок `X-Telegram-Init-Data: <Telegram.WebApp.initData>`. Userbot получает `initData` сам через `messages.requestWebView` / `messages.requestMainWebView` к боту (как при нажатии «Открыть знакомства»); из ответа берётся параметр `tgWebAppData` после `#`.
- Внутренний id пользователя (`ME.id`) отличается от Telegram id: `GET /api/user/{telegram_id}` → поле `id`.

### Эндпоинты, нужные сервису

| Запрос | Ответ / тело |
| --- | --- |
| `GET /api/user/{telegram_id}` | Свой профиль: `id`, `name`, `age`, `city`, `bio`, `interests`, `profile_extras`, `photos`, `username`, `telegram_id`, ... |
| `GET /api/likes/{me}?limit={n}&offset={k}` | Массив входящих лайков: `id`, `name`, `age`, `city`, `photos`, `is_verified`, `has_gift`, `locked`. Заголовки `X-Likes-Total`, `X-Likes-Offset-Next`, `X-Likes-Has-More` (`1`/`0`) |
| `GET /api/user/id/{id}` | Анкета: `id`, `name`, `age`, `city`, `bio`, `interests`, `height`, `profile_extras`, `photos`, `communication_language`, `dating_goal`, `gender`, `is_verified`, `last_seen`. Без `username` |
| `POST /api/like` `{from_user, to_user}` | `{match: bool, username?: string, ad?: ...}`; `429 {error: "like_limit_reached", limit, verified, verify_bonus}` (по коду: 30 в день, +20 после верификации); `403` при блокировке анкеты |
| `POST /api/likes/{me}/dismiss/{id}` | Скрыть входящий лайк (✕ во вкладке «Лайки») |
| `GET /api/matches/{me}` | Мэтчи: `id`, `name`, `age`, `photos`, `username`, `last_seen`, `match_created_at`, `is_verified` |
| `GET /api/chat/list?user_id={me}` | Внутренние чаты |
| `GET /api/chat/messages?user_a={me}&user_b={id}[&after_id=…]` | Сообщения внутреннего чата |
| `POST /api/chat/send` `{from_user, to_user, text, client_key, reply_to_id}` | `{id}`; `client_key` — ключ идемпотентности от клиента; `403 chat_unavailable`, если чат закрыт |
| `GET /api/i18n/{ru\|uz}` | `{lang, strings}`: подписи для кодов (`interest.*`, `pets.*`, `education.*`, ...) |
| `GET /photos/{file}` | Фото анкеты |

### Форматы полей

- `photos`, `interests` — JSON-массив строк в виде строки; интересы — коды (`football`, `gym`, ...), подписи в `strings["interest.<код>"]`.
- `profile_extras` — JSON-объект в виде строки: `pets`, `sport`, `education`, `family_plans`, `comm_style` (коды) и `habits` (объект флагов); может быть `null` или `{}`.
- `communication_language` — код языка общения анкеты.
- Пустые анкеты встречаются часто: у части лайков нет ни описания, ни интересов.
- Ошибки: `{"detail": {"code": …}}` (`profile_*` — ограничена ваша анкета, `chat_unavailable` — чат закрыт) или `{"error": …}` (`like_limit_reached`).

## Как DateBot использует API

| Шаг | Запросы | Код |
| --- | --- | --- |
| Вход | `messages.requestWebView` к боту (URL кнопки меню или «Открыть знакомства»), затем `GET /api/user/{telegram_id}` и `GET /api/i18n/ru` | `WTelegramTashdateClient.OpenWebAppAsync`, `TashdateAppClient` |
| Новые лайки | `GET /api/likes/{me}?limit=30&offset=…` (не больше двух страниц) | `SyncAppLikesHandler` |
| Анкета | `GET /api/user/id/{id}`, фото `GET /photos/{file}` | `ProcessAppLikeHandler`, `AppResponses.Profile` |
| Лайк в ответ | `POST /api/like`; прерванный лайк сначала ищется в `GET /api/matches/{me}` | `LikeBack` |
| Анкета к карточке мэтча | `GET /api/matches/{me}` по `@username`, затем `GET /api/user/id/{id}` | `EnrichFromApp` |
| Сообщение в чат приложения | `POST /api/chat/send` с `client_key` = id анкеты в DateBot | `GenerateAndSendReply` |

Как запросы оцениваются при сбое:

- `401` или `403` без кода — вход отклонён: `initData` запрашивается заново, запрос повторяется один раз.
- `429 like_limit_reached` — пауза лайков на час, чат бота и сообщения работают.
- `403/404 profile_*` — ваша анкета ограничена: пауза на 6 часов и уведомление.
- `5xx` и обрыв связи у `POST /api/chat/send` — исход неизвестен, сообщение не повторяется; у остальных запросов — повтор позже.
