using DateBot.Domain.Common.Errors;

namespace DateBot.Application.UseCases;

public static class OwnerErrors
{
    public static readonly Error Unauthorized = Error.Conflict(
        "owner.unauthorized", "Команда не от владельца аккаунта; игнорируется");

    public static readonly Error AccountUnknown = Error.NotFound(
        "owner.account_unknown", "Сервис ещё не вошёл в ваш Telegram-аккаунт");
}
