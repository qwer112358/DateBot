using DateBot.Domain.Common.Errors;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Inbox;

public static class InboxErrors
{
    public static readonly Error NotFinal = Error.Validation(
        "inbox_message.not_final", "Сообщение можно перевести только в финальный статус");

    public static Error AlreadyResolved(BotMessageId id, InboxStatus status) => Error.Conflict(
        "inbox_message.already_resolved", $"Сообщение {id} уже обработано (статус {status})");
}
