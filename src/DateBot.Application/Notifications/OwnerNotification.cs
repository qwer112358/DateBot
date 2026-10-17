using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.Notifications;

public enum NotificationImportance
{
    /// <summary>Sent only with verbosity <c>All</c>.</summary>
    Info,

    /// <summary>Needs your decision (Confirm mode); always sent.</summary>
    Actionable,

    /// <summary>Errors and pauses; always sent.</summary>
    Error,

    /// <summary>Answer to your command; always sent.</summary>
    Reply,
}

public abstract record OwnerNotification(NotificationImportance Importance);

/// <summary>Scenario A done: like back and/or message.</summary>
public sealed record LikeHandledNotification(MatchId MatchId, Profile Profile, bool LikedBack, ReplyText Message)
    : OwnerNotification(NotificationImportance.Info);

/// <summary>Liked back; the message waits for the mutual like.</summary>
public sealed record LikedBackNotification(MatchId MatchId, Profile Profile)
    : OwnerNotification(NotificationImportance.Info);

/// <summary>Scenario B done.</summary>
public sealed record MutualHandledNotification(MatchId MatchId, Profile Profile, ReplyText Message)
    : OwnerNotification(NotificationImportance.Info);

/// <summary>Confirm mode: profile, draft and the Send / Skip buttons.</summary>
public sealed record DraftReadyNotification(MatchId MatchId, MatchKind Kind, Profile Profile, ReplyText Draft)
    : OwnerNotification(NotificationImportance.Actionable);

public sealed record ParseFailedNotification(long BotMessageId, string RawText, string Reason)
    : OwnerNotification(NotificationImportance.Error);

public sealed record ActionFailedNotification(MatchId MatchId, Profile Profile, Error Reason)
    : OwnerNotification(NotificationImportance.Error);

public sealed record PausedNotification(TimeSpan Duration, string Reason)
    : OwnerNotification(NotificationImportance.Error);

public sealed record CatchUpNotification(int Processed, int Done, int Drafted, int Ignored, int ParseFailed, int Failed)
    : OwnerNotification(NotificationImportance.Info);

public sealed record TextNotification(string Text)
    : OwnerNotification(NotificationImportance.Reply);
