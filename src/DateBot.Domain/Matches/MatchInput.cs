using CSharpFunctionalExtensions;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Matches;

/// <param name="SourceMessageId">The bot notification the person came from; none for a like found in the app.</param>
public sealed record MatchInput(
    MatchId Id,
    TashdateUserId PersonId,
    Maybe<BotMessageId> SourceMessageId,
    MatchKind Kind,
    Profile Profile,
    DateTimeOffset ReceivedAt);
