using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Matches;

/// <summary>One entry of the action history of a <see cref="Match"/>. Changed only by the aggregate.</summary>
public sealed class MatchAction
{
    private MatchAction(MatchActionState state)
    {
        Sequence = state.Sequence;
        Type = state.Type;
        Status = state.Status;
        Text = state.Text;
        FailureReason = state.FailureReason;
        CreatedAt = state.CreatedAt;
        UpdatedAt = state.UpdatedAt;
        Anchor = state.Anchor;
        Channel = state.Channel;
    }

    public int Sequence { get; }

    public ActionType Type { get; }

    public ActionStatus Status { get; private set; }

    public Maybe<ReplyText> Text { get; private set; }

    public Maybe<string> FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>For a draft: the bot notification it answers; the bot-chat route sends the message against it.</summary>
    public Maybe<BotMessageId> Anchor { get; }

    /// <summary>For a message: where it went; a draft has none until the owner picks one.</summary>
    public Maybe<MessageChannel> Channel { get; private set; }

    /// <summary>In-memory only: the pending send came from a draft approved by the owner.</summary>
    private bool PromotedFromDraft { get; set; }

    internal static MatchAction Start(
        int sequence,
        ActionType type,
        ActionStatus status,
        Maybe<ReplyText> text,
        DateTimeOffset now,
        Maybe<BotMessageId> anchor = default,
        Maybe<MessageChannel> channel = default) =>
        new(new MatchActionState(sequence, type, status, text, Maybe<string>.None, now, now, anchor, channel));

    internal static MatchAction Restore(MatchActionState state) => new(state);

    internal void Complete(DateTimeOffset now) => Set(ActionStatus.Done, now);

    internal void Skip(DateTimeOffset now) => Set(ActionStatus.Skipped, now);

    internal void Fail(string reason, DateTimeOffset now)
    {
        FailureReason = reason;
        Set(ActionStatus.Failed, now);
    }

    /// <summary>The owner said yes to a proposed like back: from here on it is an ordinary started one.</summary>
    internal void Approve(DateTimeOffset now) => Set(ActionStatus.Pending, now);

    internal void Promote(ReplyText text, MessageChannel channel, DateTimeOffset now)
    {
        Text = text;
        Channel = channel;
        PromotedFromDraft = true;
        Set(ActionStatus.Pending, now);
    }

    /// <summary>The action did not happen: a promoted draft goes back to Draft, anything else fails.</summary>
    internal void Abort(string reason, DateTimeOffset now)
    {
        if (PromotedFromDraft)
        {
            PromotedFromDraft = false;
            FailureReason = reason;
            Channel = Maybe<MessageChannel>.None;
            Set(ActionStatus.Draft, now);
            return;
        }

        Fail(reason, now);
    }

    private void Set(ActionStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }
}
