using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Matches.States;

/// <summary>Full state of a stored match; used only to restore the aggregate from the database.</summary>
public sealed record MatchState(
    MatchId Id,
    TashdateUserId PersonId,
    Maybe<BotMessageId> SourceMessageId,
    MatchKind Kind,
    Profile Profile,
    DateTimeOffset ReceivedAt,
    MatchStatus Status,
    IReadOnlyList<MatchActionState> Actions,
    bool IsMutual);
