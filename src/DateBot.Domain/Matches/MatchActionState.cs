using CSharpFunctionalExtensions;

namespace DateBot.Domain.Matches;

public sealed record MatchActionState(
    int Sequence,
    ActionType Type,
    ActionStatus Status,
    Maybe<ReplyText> Text,
    Maybe<string> FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
