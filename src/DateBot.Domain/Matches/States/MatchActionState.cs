using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Matches.States;

public sealed record MatchActionState(
    int Sequence,
    ActionType Type,
    ActionStatus Status,
    Maybe<ReplyText> Text,
    Maybe<string> FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Maybe<BotMessageId> Anchor = default,
    Maybe<MessageChannel> Channel = default);
