using DateBot.Application.Notifications.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Notifications;

/// <summary>Liked back; the message waits for the mutual like.</summary>
public sealed record LikedBackNotification(MatchId MatchId, Profile Profile)
    : OwnerNotification(NotificationImportance.Info);
