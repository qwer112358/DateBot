using DateBot.Domain.Matches;

namespace DateBot.Application.Notifications;

/// <summary>Confirm mode: profile, draft and the Send / Skip buttons.</summary>
public sealed record DraftReadyNotification(MatchId MatchId, MatchKind Kind, Profile Profile, ReplyText Draft)
    : OwnerNotification(NotificationImportance.Actionable);
