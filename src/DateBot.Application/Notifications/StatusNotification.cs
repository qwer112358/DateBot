using DateBot.Application.Notifications.Enums;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.Notifications;

public sealed record StatusNotification(StatusReport Report) : OwnerNotification(NotificationImportance.Reply);
