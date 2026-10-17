using DateBot.Application.Notifications.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Notifications;

/// <summary>A new like in the app: profile, photo, and your yes or no on the like back.</summary>
public sealed record LikeBackProposedNotification(MatchId MatchId, Profile Profile)
    : OwnerNotification(NotificationImportance.Actionable);
