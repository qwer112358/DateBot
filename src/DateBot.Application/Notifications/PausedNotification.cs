using DateBot.Application.Notifications.Enums;

namespace DateBot.Application.Notifications;

public sealed record PausedNotification(TimeSpan Duration, string Reason)
    : OwnerNotification(NotificationImportance.Error);
