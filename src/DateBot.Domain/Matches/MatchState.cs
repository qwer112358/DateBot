using DateBot.Domain.Shared;

namespace DateBot.Domain.Matches;

/// <summary>Full state of a stored match; used only to restore the aggregate from the database.</summary>
public sealed record MatchState(
    MatchId Id,
    TashdateUserId PersonId,
    BotMessageId SourceMessageId,
    MatchKind Kind,
    Profile Profile,
    DateTimeOffset ReceivedAt,
    MatchStatus Status,
    IReadOnlyList<MatchActionState> Actions);
