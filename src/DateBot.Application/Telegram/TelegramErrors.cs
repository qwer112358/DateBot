using DateBot.Application.Abstractions;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Application.Telegram;

/// <summary>Errors the Telegram adapters return through the ports.</summary>
public static class TelegramErrors
{
    public static readonly Error NotConnected = Error.External(
        "telegram.connection.unavailable", "Клиент Telegram ещё не подключён");

    public static readonly Error BotNotFound = Error.NotFound(
        "telegram.bot_not_found", "Не удалось найти чат с ботом Tashdate: проверьте имя бота в конфиге");

    public static Error FloodWait(int seconds) => Error.RateLimited(
        "telegram.flood_wait", $"Telegram попросил подождать {seconds} с (FLOOD_WAIT)", TimeSpan.FromSeconds(seconds));

    public const string ButtonNotFoundCode = "telegram.button_not_found";

    /// <summary>The bot has no Mini App button to open, so the app cannot be signed into.</summary>
    public static readonly Error WebAppNotFound = Error.NotFound(
        "telegram.web_app_not_found", "У бота не найдена кнопка Mini App; укажите адрес в MiniAppClient:Url");

    /// <summary>Retryable: the request did not reach Telegram, nothing was sent.</summary>
    public static Error Unavailable(string details) => Error.External(
        "telegram.unavailable", $"Telegram недоступен: {details}");

    /// <summary>Not retryable: the connection broke after the text was handed over, it may already be delivered.</summary>
    public static Error SendOutcomeUnknown(string details) => DeliveryErrors.OutcomeUnknown(details);

    /// <summary>Plain text to the bot goes to the profile it shows now; the anchor is no longer that profile.</summary>
    public static Error StaleAnchor(BotMessageId id) => Error.Conflict(
        "telegram.stale_anchor", $"Бот уже показывает анкету новее сообщения {id}; текст не отправлен, чтобы не написать другому человеку");

    public static Error Rpc(int code, string type) => Error.External(
        "telegram.rpc", $"Telegram вернул ошибку {code} {type}");

    public static Error MessageNotFound(BotMessageId id) => Error.NotFound(
        "telegram.message_not_found", $"Сообщение бота {id} не найдено");

    public static Error ButtonNotFound(BotMessageId id, IEnumerable<string> labels) => Error.NotFound(
        ButtonNotFoundCode, $"В сообщении {id} нет кнопки из списка: {string.Join(", ", labels)}");

    public static Error UserNotFound(ContactUsername username) => Error.NotFound(
        "telegram.user_not_found", $"Пользователь {username} не найден");
}
