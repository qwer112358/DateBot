using CSharpFunctionalExtensions;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Inbox;

public sealed record InboxMessageState(
    BotMessageId Id, string RawText, DateTimeOffset SentAt, InboxStatus Status, Maybe<string> Note, DateTimeOffset UpdatedAt);
