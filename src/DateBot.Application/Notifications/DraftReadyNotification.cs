using System.Collections.Immutable;
using DateBot.Application.Notifications.Enums;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Notifications;

/// <summary>Confirm mode: profile, draft, one button per channel that reaches the person, and Skip.</summary>
/// <param name="Mutual">Already a match (a like back in the app makes one at once).</param>
/// <param name="Channels">Where the draft can go, preferred first.</param>
/// <param name="WithAlbum">Show all the profile's photos above the card: the first card about the person.</param>
public sealed record DraftReadyNotification(
    MatchId MatchId,
    MatchKind Kind,
    Profile Profile,
    ReplyText Draft,
    bool Mutual,
    ImmutableArray<MessageChannel> Channels,
    bool WithAlbum = false)
    : OwnerNotification(NotificationImportance.Actionable);
