using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.Events;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;
using static DateBot.Domain.Tests.Support.TestData;

namespace DateBot.Domain.Tests.Matches;

/// <summary>A person met through the app: no bot message, a match made by our like back, contacts learned later.</summary>
public sealed class AppLikeMatchTests
{
    [Fact]
    public void Like_from_the_app_has_no_source_message_and_is_not_mutual_yet()
    {
        var match = AppLike();

        match.SourceMessageId.HasNoValue.Should().BeTrue();
        match.IsMutual.Should().BeFalse();
        match.Profile.AppId.Should().Be(Maybe.From(AppProfileId.Create(501).Value));
    }

    [Fact]
    public void Like_back_that_made_a_match_says_so_in_the_event()
    {
        var match = AppLike();
        match.PullDomainEvents();

        match.StartLikeBack(Now)
            .Bind(() => match.ConfirmMutual())
            .Bind(() => match.CompleteLikeBack(Now))
            .IsSuccess.Should().BeTrue();

        match.IsMutual.Should().BeTrue();
        match.CanStartMessage.Should().BeTrue();
        match.PullDomainEvents().Should().ContainSingle().Which.Should().BeOfType<LikeBackSent>().Which.Mutual.Should().BeTrue();
    }

    [Fact]
    public void Mutual_notification_starts_mutual()
    {
        NewMatch(MatchKind.Mutual).IsMutual.Should().BeTrue();
        NewMatch(MatchKind.LikedMe).IsMutual.Should().BeFalse();
    }

    [Fact]
    public void Reveal_contact_adds_what_is_missing_and_keeps_what_is_known()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.RevealContact(Maybe.From(ContactUsername.Create("first_name").Value), Maybe.From(AppProfileId.Create(7).Value));
        match.RevealContact(Maybe.From(ContactUsername.Create("other_name").Value), Maybe.From(AppProfileId.Create(8).Value));

        match.Profile.Username.GetValueOrThrow().Value.Should().Be("first_name");
        match.Profile.AppId.GetValueOrThrow().Value.Should().Be(7);
    }

    [Fact]
    public void Enrich_fills_the_short_card_from_the_app_profile()
    {
        var card = Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value,
            TashdateUserId.Create("id:lola_tash").Value,
            BotMessageId.Create(40).Value,
            MatchKind.Mutual,
            Domain.Matches.ValueObjects.Profile.Create(new ProfileInput(
                ProfileName.Create("Лола").Value, Maybe<Age>.None, Maybe<City>.None, Maybe<Bio>.None, Interests.Empty,
                Maybe.From(PhotoFileId.Create("photo:222").Value), Maybe.From(ContactUsername.Create("lola_tash").Value),
                "❤️ У вас мэтч с Лола!")).Value,
            Now)).Value;

        card.Enrich(AppLike().Profile);

        var profile = card.Profile;
        profile.Name.Value.Should().Be("Лола");
        profile.Age.GetValueOrThrow().Value.Should().Be(23);
        profile.Interests.Items.Should().Equal("кофе");
        profile.Facts.Items.Should().Equal("Рост: 165 см");
        profile.AppId.GetValueOrThrow().Value.Should().Be(501);
        profile.Photo.GetValueOrThrow().Value.Should().Be("photo:222", "the card's own photo stays");
        profile.Username.GetValueOrThrow().Value.Should().Be("lola_tash");
        profile.RawText.Should().StartWith("❤️");
    }

    [Fact]
    public void Decline_closes_a_new_person_only()
    {
        var fresh = AppLike();
        fresh.Decline().IsSuccess.Should().BeTrue();
        fresh.Status.Should().Be(MatchStatus.Skipped);

        var started = AppLike();
        started.StartLikeBack(Now);
        started.Decline().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Proposed_like_back_waits_as_a_draft_and_asks_for_a_card()
    {
        var match = AppLike();
        match.PullDomainEvents();

        match.ProposeLikeBack(Now).IsSuccess.Should().BeTrue();

        match.LikeBackProposal.HasValue.Should().BeTrue();
        match.PendingAction.HasNoValue.Should().BeTrue();
        match.Status.Should().Be(MatchStatus.Received);
        match.PullDomainEvents().Should().ContainSingle().Which.Should().BeOfType<LikeBackProposed>();
    }

    [Fact]
    public void Approved_proposal_becomes_the_like_back_itself()
    {
        var match = AppLike();
        match.ProposeLikeBack(Now);

        match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now)).IsSuccess.Should().BeTrue();

        match.Actions.Should().ContainSingle().Which.Status.Should().Be(ActionStatus.Done);
        match.HasLikedBack.Should().BeTrue();
        match.LikeBackProposal.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Skipped_proposal_closes_the_person_without_a_like()
    {
        var match = AppLike();
        match.ProposeLikeBack(Now);

        match.SkipLikeBack(Now).IsSuccess.Should().BeTrue();

        match.Status.Should().Be(MatchStatus.Skipped);
        match.Actions.Single().Status.Should().Be(ActionStatus.Skipped);
        match.SkipLikeBack(Now).Error.Code.Should().Be("match.no_like_back_proposal");
    }

    [Fact]
    public void Like_back_is_proposed_only_once_and_only_to_a_new_liker()
    {
        var proposed = AppLike();
        proposed.ProposeLikeBack(Now);
        proposed.ProposeLikeBack(Now).IsFailure.Should().BeTrue();

        var started = AppLike();
        started.StartLikeBack(Now);
        started.ProposeLikeBack(Now).IsFailure.Should().BeTrue();

        NewMatch(MatchKind.Mutual).ProposeLikeBack(Now).Error.Should().Be(MatchErrors.LikeBackNotAllowed);
    }

    [Fact]
    public void Restore_rejects_two_like_backs_for_one_person()
    {
        var match = AppLike();
        MatchActionState LikeBack(int sequence, ActionStatus status) =>
            new(sequence, ActionType.LikeBack, status, Maybe<ReplyText>.None, Maybe<string>.None, Now, Now, Maybe<BotMessageId>.None, Maybe<MessageChannel>.None);

        var restored = Match.Restore(new MatchState(
            match.Id, match.PersonId, match.SourceMessageId, match.Kind, match.Profile, match.ReceivedAt, MatchStatus.Received,
            [LikeBack(1, ActionStatus.Draft), LikeBack(2, ActionStatus.Pending)], match.IsMutual));

        restored.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Message_remembers_its_channel()
    {
        var match = AppLike();
        match.StartLikeBack(Now).Bind(() => match.ConfirmMutual()).Bind(() => match.CompleteLikeBack(Now));
        match.PullDomainEvents();

        match.StartMessage(Reply(), MessageChannel.App, Now).Bind(() => match.CompleteMessage(Now)).IsSuccess.Should().BeTrue();

        match.Actions.Single(a => a.Type == ActionType.SendMessage).Channel.Should().Be(Maybe.From(MessageChannel.App));
        match.PullDomainEvents().OfType<MessageSent>().Single().Channel.Should().Be(MessageChannel.App);
    }

    [Fact]
    public void Aborted_draft_forgets_the_channel_it_was_sent_to()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.DraftReply(Reply(), Maybe<BotMessageId>.None, Now);
        match.StartMessage(Reply(), MessageChannel.Telegram, Now);

        match.AbortPendingAction(Error.Conflict("telegram.rpc", "USER_PRIVACY_RESTRICTED"), Now);

        match.DraftMessage.GetValueOrThrow().Channel.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Identity_keys_cover_every_door_the_person_came_through()
    {
        var match = AppLike(501);
        match.RevealContact(Maybe.From(ContactUsername.Create("Lola_Tash").Value));

        match.IdentityKeys.Should().BeEquivalentTo("app:501", "tg:lola_tash");
    }

    [Fact]
    public void Restore_keeps_a_match_without_source_and_with_a_channel()
    {
        var original = AppLike();
        original.StartLikeBack(Now).Bind(() => original.ConfirmMutual()).Bind(() => original.CompleteLikeBack(Now))
            .Bind(() => original.StartMessage(Reply(), MessageChannel.App, Now));

        var restored = Match.Restore(new MatchState(
            original.Id, original.PersonId, original.SourceMessageId, original.Kind, original.Profile, original.ReceivedAt, original.Status,
            [.. original.Actions.Select(a => new MatchActionState(
                a.Sequence, a.Type, a.Status, a.Text, a.FailureReason, a.CreatedAt, a.UpdatedAt, a.Anchor, a.Channel))],
            original.IsMutual)).Value;

        restored.SourceMessageId.HasNoValue.Should().BeTrue();
        restored.IsMutual.Should().BeTrue();
        restored.PendingAction.GetValueOrThrow().Channel.Should().Be(Maybe.From(MessageChannel.App));
    }
}
