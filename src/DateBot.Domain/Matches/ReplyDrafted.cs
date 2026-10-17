using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Matches;

/// <param name="Anchor">The bot notification the draft answers, for the bot-chat channel.</param>
public sealed record ReplyDrafted(
    MatchId MatchId,
    MatchKind Kind,
    Profile Profile,
    ReplyText Text,
    bool Mutual,
    Maybe<BotMessageId> Anchor,
    DateTimeOffset OccurredAt) : IDomainEvent;
