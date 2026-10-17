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

    public static Error Unavailable(string details) => Error.External(
        "telegram.unavailable", $"Telegram недоступен: {details}");

    public static Error Rpc(int code, string type) => Error.External(
        "telegram.rpc", $"Telegram вернул ошибку {code} {type}");

    public static Error MessageNotFound(BotMessageId id) => Error.NotFound(
        "telegram.message_not_found", $"Сообщение бота {id} не найдено");

    public static Error ButtonNotFound(BotMessageId id, IEnumerable<string> labels) => Error.NotFound(
        "telegram.button_not_found", $"В сообщении {id} нет кнопки из списка: {string.Join(", ", labels)}");

    public static Error UserNotFound(ContactUsername username) => Error.NotFound(
        "telegram.user_not_found", $"Пользователь {username} не найден");
}
