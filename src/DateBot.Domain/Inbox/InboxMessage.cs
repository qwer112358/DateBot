using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Inbox.States;
using DateBot.Domain.Inbox.Validators;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Inbox;

/// <summary>
/// A raw message from the Tashdate bot as it arrived. Kept for every message so that nothing is lost
/// and <see cref="InboxStatus.ParseFailed"/> messages can be reviewed by hand.
/// </summary>
public sealed class InboxMessage
{
    public const int MaxRawTextLength = 32_000;
    private static readonly InboxMessageStateValidator Validator = new();

    private InboxMessage(InboxMessageState state)
    {
        Id = state.Id;
        RawText = state.RawText;
        SentAt = state.SentAt;
        Status = state.Status;
        Note = state.Note;
        UpdatedAt = state.UpdatedAt;
    }

    public BotMessageId Id { get; }

    public string RawText { get; }

    public DateTimeOffset SentAt { get; }

    public InboxStatus Status { get; private set; }

    public Maybe<string> Note { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsResolved => Status != InboxStatus.Received;

    public static Result<InboxMessage, Error> Receive(BotMessageId id, string rawText, DateTimeOffset sentAt, DateTimeOffset now) =>
        Restore(new InboxMessageState(id, rawText, sentAt, InboxStatus.Received, Maybe<string>.None, now));

    public static Result<InboxMessage, Error> Restore(InboxMessageState state) =>
        Validator.ValidateAndCreate(state, "inbox_message.invalid", s => new InboxMessage(s));

    /// <summary>Moves the message to a final status; a resolved message cannot change any more.</summary>
    public UnitResult<Error> Resolve(InboxStatus final, Maybe<string> note, DateTimeOffset now) =>
        Guard.Require(!IsResolved, InboxErrors.AlreadyResolved(Id, Status))
            .Bind(() => Guard.Require(final != InboxStatus.Received, InboxErrors.NotFinal))
            .Tap(() =>
            {
                Status = final;
                Note = note;
                UpdatedAt = now;
            });
}
