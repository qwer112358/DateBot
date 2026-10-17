using CSharpFunctionalExtensions;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Inbox.States;

public sealed record InboxMessageState(
    BotMessageId Id, string RawText, DateTimeOffset SentAt, InboxStatus Status, Maybe<string> Note, DateTimeOffset UpdatedAt);
