namespace DateBot.Application.Notifications;

public sealed record ParseFailedNotification(long BotMessageId, string RawText, string Reason)
    : OwnerNotification(NotificationImportance.Error);
