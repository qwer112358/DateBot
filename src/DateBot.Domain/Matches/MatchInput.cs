using DateBot.Domain.Shared;

namespace DateBot.Domain.Matches;

public sealed record MatchInput(
    MatchId Id,
    TashdateUserId PersonId,
    BotMessageId SourceMessageId,
    MatchKind Kind,
    Profile Profile,
    DateTimeOffset ReceivedAt);
