using System.Collections.Immutable;
using DateBot.Domain.Matches;

namespace DateBot.Application.Notifications;

/// <summary>Confirm mode: profile, draft, one button per channel that reaches the person, and Skip.</summary>
/// <param name="Mutual">Already a match (a like back in the app makes one at once).</param>
/// <param name="Channels">Where the draft can go, preferred first.</param>
public sealed record DraftReadyNotification(
    MatchId MatchId,
    MatchKind Kind,
    Profile Profile,
    ReplyText Draft,
    bool Mutual,
    ImmutableArray<MessageChannel> Channels)
    : OwnerNotification(NotificationImportance.Actionable);
