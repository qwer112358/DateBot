using DateBot.Application.Notifications;

namespace DateBot.Application.UseCases;

public sealed record StatusNotification(StatusReport Report) : OwnerNotification(NotificationImportance.Reply);
