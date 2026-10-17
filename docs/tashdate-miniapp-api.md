# TashDate: бот и Mini App

Что известно о `@Znakomstva_Tashkentt_bot` на 2026-10-17. API выяснен по публичному коду Mini App и по GET-запросам на чтение от аккаунта владельца. Лайки, сообщения и другие изменяющие запросы при исследовании не отправлялись, поэтому их тела взяты из кода приложения, а ответы на них не проверены вживую.

## Сообщения бота в чате

| Событие | Текст | Кнопка |
| --- | --- | --- |
| Мэтч | Фото + «❤️ У вас мэтч с {имя}!\n\nВы оба лайкнули друг друга. Самое время написать.\n@{username}» | «💛 Открыть знакомства» (web app) |
| Кто-то лайкнул | «Тобой интересуются\n\nЗагляни в приложение — вдруг это взаимно.» | «💛 Открыть знакомства» |
| Сводка лайков | «{Имя владельца}, тебя лайкнули N раза за последние 24 часа\n\nОткрой приложение — посмотри кто и ответь лайком» | «Посмотреть кто лайкнул» |

Уведомления о лайках анкету не содержат: кто лайкнул, видно только в Mini App.

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
