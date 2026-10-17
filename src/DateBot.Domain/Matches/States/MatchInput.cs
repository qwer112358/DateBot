using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Matches.States;

/// <param name="SourceMessageId">The bot notification the person came from; none for a like found in the app.</param>
public sealed record MatchInput(
    MatchId Id,
    TashdateUserId PersonId,
    Maybe<BotMessageId> SourceMessageId,
    MatchKind Kind,
    Profile Profile,
    DateTimeOffset ReceivedAt);
