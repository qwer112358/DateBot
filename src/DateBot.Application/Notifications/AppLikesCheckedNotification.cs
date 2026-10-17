using DateBot.Application.Notifications.Enums;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.Notifications;

/// <summary>Answer to /likes once the app has been checked.</summary>
public sealed record AppLikesCheckedNotification(AppSyncReport Report) : OwnerNotification(NotificationImportance.Reply);
