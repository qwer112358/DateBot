using DateBot.Application.Notifications.Enums;

namespace DateBot.Application.Notifications;

public sealed record CatchUpNotification(int Processed, int Done, int Drafted, int Ignored, int ParseFailed, int Failed)
    : OwnerNotification(NotificationImportance.Info);
