using DateBot.Application.Notifications.Enums;

namespace DateBot.Application.Notifications;

public sealed record TextNotification(string Text)
    : OwnerNotification(NotificationImportance.Reply);
