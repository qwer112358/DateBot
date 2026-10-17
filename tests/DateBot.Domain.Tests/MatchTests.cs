using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using static DateBot.Domain.Tests.TestData;

namespace DateBot.Domain.Tests;

public sealed class MatchTests
{
    [Fact]
    public void Create_raises_the_event_matching_the_kind()
    {
        NewMatch(MatchKind.LikedMe).DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LikeReceived>();
        NewMatch(MatchKind.Mutual).DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MutualMatchReceived>();
    }

    [Fact]
    public void Scenario_A_goes_through_all_statuses()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.PullDomainEvents();

        var result = match.StartLikeBack(Now)
            .Bind(() => match.CompleteLikeBack(Now))
            .Bind(() => match.StartMessage(Reply(), MessageChannel.Telegram, Now))
            .Bind(() => match.CompleteMessage(Now))
            .Bind(() => match.Close());

        result.IsSuccess.Should().BeTrue();
        match.Status.Should().Be(MatchStatus.Done);
        match.Actions.Select(a => (a.Type, a.Status)).Should().Equal(
            (ActionType.LikeBack, ActionStatus.Done),
            (ActionType.SendMessage, ActionStatus.Done));
        var events = match.PullDomainEvents();
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<LikeBackSent>();
        events[1].Should().BeOfType<MessageSent>().Which.LikedBack.Should().BeTrue();
    }

    [Fact]
    public void Scenario_B_messages_without_like()
    {
        var match = NewMatch(MatchKind.Mutual);

        match.StartMessage(Reply(), MessageChannel.Telegram, Now).Bind(() => match.CompleteMessage(Now)).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(MatchStatus.MessageSent);
        match.Actions.Should().ContainSingle(a => a.Type == ActionType.SendMessage);
    }

    [Fact]
    public void Like_back_is_only_for_incoming_likes()
    {
        var match = NewMatch(MatchKind.Mutual);

        match.StartLikeBack(Now).Error.Should().Be(MatchErrors.LikeBackNotAllowed);
    }

    [Fact]
    public void Liked_me_cannot_be_messaged_before_the_like_back()
    {
        var match = NewMatch(MatchKind.LikedMe);

        match.StartMessage(Reply(), MessageChannel.Telegram, Now).Error.Code.Should().Be("match.invalid_state");
    }

    [Fact]
    public void Only_one_auto_message_per_person()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.StartMessage(Reply(), MessageChannel.Telegram, Now).Bind(() => match.CompleteMessage(Now));

        match.StartMessage(Reply("Ещё одно сообщение"), MessageChannel.Telegram, Now).Error.Should().Be(MatchErrors.AlreadyMessaged);
        match.DraftReply(Reply("Черновик"), Anchor, Now).Error.Should().Be(MatchErrors.AlreadyMessaged);
    }

    [Fact]
    public void Closed_match_cannot_fail()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.StartMessage(Reply(), MessageChannel.Telegram, Now).Bind(() => match.CompleteMessage(Now)).Bind(() => match.Close());

        match.Fail(Error.External("x", "y"), Now).Error.Code.Should().Be("match.invalid_transition");
    }

    [Fact]
    public void Fail_marks_pending_action_and_raises_event()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now);
        match.PullDomainEvents();

        match.Fail(Error.External("telegram.rpc", "boom"), Now).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(MatchStatus.Failed);
        match.Actions.Single().Status.Should().Be(ActionStatus.Failed);
        match.PullDomainEvents().Should().ContainSingle().Which.Should().BeOfType<ProcessingFailed>();
    }

    [Fact]
    public void Start_like_back_twice_resumes_the_pending_action()
    {
        var match = NewMatch(MatchKind.LikedMe);

        match.StartLikeBack(Now);
        match.StartLikeBack(Now).IsSuccess.Should().BeTrue();

        match.Actions.Should().ContainSingle();
    }

    [Fact]
    public void Aborted_send_can_be_retried()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.StartMessage(Reply(), MessageChannel.Telegram, Now);

        match.AbortPendingAction(Error.RateLimited("telegram.flood_wait", "wait", TimeSpan.FromSeconds(30)), Now);

        match.HasActiveMessage.Should().BeFalse();
        match.StartMessage(Reply(), MessageChannel.Telegram, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Draft_can_be_sent_and_returns_to_draft_when_send_is_aborted()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.DraftReply(Reply(), Anchor, Now).IsSuccess.Should().BeTrue();
        match.DraftMessage.GetValueOrThrow().Anchor.Should().Be(Maybe.From(Anchor));
        match.DomainEvents.OfType<ReplyDrafted>().Should().ContainSingle();

        match.StartMessage(Reply(), MessageChannel.Telegram, Now).IsSuccess.Should().BeTrue();
        match.AbortPendingAction(Error.RateLimited("telegram.flood_wait", "wait", TimeSpan.FromSeconds(30)), Now);

        match.DraftMessage.HasValue.Should().BeTrue();
        match.StartMessage(Reply(), MessageChannel.Telegram, Now).Bind(() => match.CompleteMessage(Now)).IsSuccess.Should().BeTrue();
        match.Actions.Should().ContainSingle(a => a.Type == ActionType.SendMessage && a.Status == ActionStatus.Done);
    }

    [Fact]
    public void Skipping_a_draft_closes_the_match()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.DraftReply(Reply(), Anchor, Now);

        match.SkipReply(Now).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(MatchStatus.Skipped);
        match.Actions.Single().Status.Should().Be(ActionStatus.Skipped);
    }

    [Fact]
    public void Restore_rebuilds_state_without_events()
    {
        var original = NewMatch(MatchKind.LikedMe);
        original.StartLikeBack(Now).Bind(() => original.CompleteLikeBack(Now));

        var restored = Match.Restore(new MatchState(
            original.Id, original.PersonId, original.SourceMessageId, original.Kind, original.Profile, original.ReceivedAt,
            original.Status,
            [.. original.Actions.Select(a => new MatchActionState(a.Sequence, a.Type, a.Status, a.Text, a.FailureReason, a.CreatedAt, a.UpdatedAt))],
            original.IsMutual));

        restored.IsSuccess.Should().BeTrue();
        restored.Value.Status.Should().Be(MatchStatus.LikedBack);
        restored.Value.HasLikedBack.Should().BeTrue();
        restored.Value.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Restore_rejects_two_messages_for_one_person()
    {
        var reply = Maybe.From(Reply());
        var state = new MatchState(
            MatchId.Create(Guid.NewGuid()).Value, TashdateUserId.Create("fp:x").Value, Shared.BotMessageId.Create(1).Value,
            MatchKind.Mutual, Profile(), Now, MatchStatus.MessageSent,
            [
                new MatchActionState(1, ActionType.SendMessage, ActionStatus.Done, reply, Maybe<string>.None, Now, Now),
                new MatchActionState(2, ActionType.SendMessage, ActionStatus.Done, reply, Maybe<string>.None, Now, Now),
            ],
            IsMutual: true);

        var restored = Match.Restore(state);

        restored.IsFailure.Should().BeTrue();
        restored.Error.Details.Select(d => d.ErrorCode).Should().Contain("match.many_messages");
    }

    [Fact]
    public void Restore_rejects_like_back_on_mutual()
    {
        var state = new MatchState(
            MatchId.Create(Guid.NewGuid()).Value, TashdateUserId.Create("fp:x").Value, Shared.BotMessageId.Create(1).Value,
            MatchKind.Mutual, Profile(), Now, MatchStatus.LikedBack,
            [new MatchActionState(1, ActionType.LikeBack, ActionStatus.Done, Maybe<ReplyText>.None, Maybe<string>.None, Now, Now)],
            IsMutual: true);

        Match.Restore(state).IsFailure.Should().BeTrue();
    }
}
