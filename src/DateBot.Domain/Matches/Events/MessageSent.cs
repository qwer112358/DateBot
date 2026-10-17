using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.Events;

public sealed record MessageSent(
    MatchId MatchId,
    MatchKind Kind,
    Profile Profile,
    ReplyText Text,
    MessageChannel Channel,
    bool LikedBack,
    DateTimeOffset OccurredAt) : IDomainEvent;
