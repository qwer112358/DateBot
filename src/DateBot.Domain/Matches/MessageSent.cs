using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record MessageSent(
    MatchId MatchId,
    MatchKind Kind,
    Profile Profile,
    ReplyText Text,
    MessageChannel Channel,
    bool LikedBack,
    DateTimeOffset OccurredAt) : IDomainEvent;
