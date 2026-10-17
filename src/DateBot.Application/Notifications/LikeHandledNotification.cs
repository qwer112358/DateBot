using DateBot.Domain.Matches;

namespace DateBot.Application.Notifications;

/// <summary>Scenario A done: like back and/or message.</summary>
public sealed record LikeHandledNotification(MatchId MatchId, Profile Profile, bool LikedBack, ReplyText Message)
    : OwnerNotification(NotificationImportance.Info);
