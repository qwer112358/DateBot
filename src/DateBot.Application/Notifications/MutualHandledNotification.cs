using DateBot.Domain.Matches;

namespace DateBot.Application.Notifications;

/// <summary>Scenario B done.</summary>
public sealed record MutualHandledNotification(MatchId MatchId, Profile Profile, ReplyText Message, MessageChannel Channel)
    : OwnerNotification(NotificationImportance.Info);
