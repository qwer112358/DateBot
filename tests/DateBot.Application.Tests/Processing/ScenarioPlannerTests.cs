using CSharpFunctionalExtensions;
using DateBot.Application.Processing.Plans;
using DateBot.Application.Processing.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Application.Tests.Processing;

public sealed class ScenarioPlannerTests
{
    private static readonly BotMessageId Source = BotMessageId.Create(10).Value;
    private static readonly BotMessageId Other = BotMessageId.Create(11).Value;
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Whether the message may go before the match is the message step's call (policy, channels).</summary>
    [Fact]
    public void New_like_is_liked_back_and_offered_to_the_message_step() =>
        ScenarioPlanner.Plan(MatchKind.LikedMe, Source, Maybe<Match>.None)
            .Should().Be(new ExecutePlan(CreateMatch: true, LikeBack: true, Message: true));

    [Fact]
    public void New_mutual_gets_only_a_message() =>
        ScenarioPlanner.Plan(MatchKind.Mutual, Source, Maybe<Match>.None)
            .Should().Be(new ExecutePlan(CreateMatch: true, LikeBack: false, Message: true));

    [Fact]
    public void Closed_match_is_ignored()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.Fail(Error.External("x", "y"), Now);

        ScenarioPlanner.Plan(MatchKind.Mutual, Other, match).Should().BeOfType<IgnorePlan>();
    }

    [Fact]
    public void Mutual_after_like_back_triggers_the_deferred_message()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now));

        ScenarioPlanner.Plan(MatchKind.Mutual, Other, match)
            .Should().Be(new ExecutePlan(CreateMatch: false, LikeBack: false, Message: true));
    }

    [Fact]
    public void Repeated_like_is_ignored()
    {
        var match = NewMatch(MatchKind.LikedMe);

        ScenarioPlanner.Plan(MatchKind.LikedMe, Other, match).Should().BeOfType<IgnorePlan>();
    }

    [Fact]
    public void Resume_repeats_only_unfinished_steps()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now));

        ScenarioPlanner.Plan(MatchKind.LikedMe, Source, match)
            .Should().Be(new ExecutePlan(CreateMatch: false, LikeBack: false, Message: true));
    }

    [Fact]
    public void Resume_with_pending_like_repeats_the_like()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now);

        ScenarioPlanner.Plan(MatchKind.LikedMe, Source, match)
            .Should().Be(new ExecutePlan(CreateMatch: false, LikeBack: true, Message: true));
    }

    [Fact]
    public void Like_from_the_app_seen_again_resumes()
    {
        var match = NewMatch(MatchKind.LikedMe, source: Maybe<BotMessageId>.None);
        match.StartLikeBack(Now);

        ScenarioPlanner.Plan(MatchKind.LikedMe, Maybe<BotMessageId>.None, match)
            .Should().Be(new ExecutePlan(CreateMatch: false, LikeBack: true, Message: true));
    }

    [Fact]
    public void Match_card_after_a_drafted_app_like_is_ignored()
    {
        var match = NewMatch(MatchKind.LikedMe, source: Maybe<BotMessageId>.None);
        match.StartLikeBack(Now).Bind(() => match.ConfirmMutual()).Bind(() => match.CompleteLikeBack(Now))
            .Bind(() => match.DraftReply(ReplyText.Create("Привет! Как проходит твой день?").Value, Maybe<BotMessageId>.None, Now));

        ScenarioPlanner.Plan(MatchKind.Mutual, Other, match).Should().BeOfType<IgnorePlan>();
    }

    private static Match NewMatch(MatchKind kind, Maybe<BotMessageId>? source = null) =>
        Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value,
            TashdateUserId.Create("fp:anna").Value,
            source ?? Source,
            kind,
            Profile.Create(new ProfileInput(
                ProfileName.Create("Анна").Value, Maybe<Age>.None, Maybe<City>.None, Maybe<Bio>.None,
                Interests.Empty, Maybe<PhotoFileId>.None, Maybe<ContactUsername>.None, "Анна")).Value,
            Now)).Value;
}
