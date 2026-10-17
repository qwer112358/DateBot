using DateBot.Application.Notifications.Enums;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Notifications;

/// <summary>Scenario A done: like back and/or message.</summary>
public sealed record LikeHandledNotification(MatchId MatchId, Profile Profile, bool LikedBack, ReplyText Message, MessageChannel Channel)
    : OwnerNotification(NotificationImportance.Info);
