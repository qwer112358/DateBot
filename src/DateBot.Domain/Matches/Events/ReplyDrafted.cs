using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Matches.Events;

/// <param name="Anchor">The bot notification the draft answers, for the bot-chat channel.</param>
public sealed record ReplyDrafted(
    MatchId MatchId,
    MatchKind Kind,
    Profile Profile,
    ReplyText Text,
    bool Mutual,
    Maybe<BotMessageId> Anchor,
    DateTimeOffset OccurredAt) : IDomainEvent;
