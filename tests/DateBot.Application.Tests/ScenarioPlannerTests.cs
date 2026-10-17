using CSharpFunctionalExtensions;
using DateBot.Application.Processing;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Application.Tests;

public sealed class ScenarioPlannerTests
{
    private static readonly BotMessageId Source = BotMessageId.Create(10).Value;
    private static readonly BotMessageId Other = BotMessageId.Create(11).Value;
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(LikedMeMessagePolicy.AfterLikeBack, true)]
    [InlineData(LikedMeMessagePolicy.OnMutual, false)]
    public void New_like_is_answered_by_policy(LikedMeMessagePolicy policy, bool message) =>
        ScenarioPlanner.Plan(MatchKind.LikedMe, Maybe<Match>.None, Source, policy)
            .Should().Be(new ExecutePlan(CreateMatch: true, LikeBack: true, Message: message));

    [Fact]
    public void New_mutual_gets_only_a_message() =>
        ScenarioPlanner.Plan(MatchKind.Mutual, Maybe<Match>.None, Source, LikedMeMessagePolicy.AfterLikeBack)
            .Should().Be(new ExecutePlan(CreateMatch: true, LikeBack: false, Message: true));

    [Fact]
    public void Closed_match_is_ignored()
    {
        var match = NewMatch(MatchKind.Mutual);
        match.Fail(Domain.Common.Error.External("x", "y"), Now);

        ScenarioPlanner.Plan(MatchKind.Mutual, match, Other, LikedMeMessagePolicy.AfterLikeBack).Should().BeOfType<IgnorePlan>();
    }

    [Fact]
    public void Mutual_after_like_back_triggers_the_deferred_message()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now));

        ScenarioPlanner.Plan(MatchKind.Mutual, match, Other, LikedMeMessagePolicy.OnMutual)
            .Should().Be(new ExecutePlan(CreateMatch: false, LikeBack: false, Message: true));
    }

    [Fact]
    public void Repeated_like_is_ignored()
    {
        var match = NewMatch(MatchKind.LikedMe);

        ScenarioPlanner.Plan(MatchKind.LikedMe, match, Other, LikedMeMessagePolicy.AfterLikeBack).Should().BeOfType<IgnorePlan>();
    }

    [Fact]
    public void Resume_repeats_only_unfinished_steps()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now));

        ScenarioPlanner.Plan(MatchKind.LikedMe, match, Source, LikedMeMessagePolicy.AfterLikeBack)
            .Should().Be(new ExecutePlan(CreateMatch: false, LikeBack: false, Message: true));
    }

    [Fact]
    public void Resume_with_pending_like_repeats_the_like()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now);

        ScenarioPlanner.Plan(MatchKind.LikedMe, match, Source, LikedMeMessagePolicy.OnMutual)
            .Should().Be(new ExecutePlan(CreateMatch: false, LikeBack: true, Message: false));
    }

    private static Match NewMatch(MatchKind kind) =>
        Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value,
            TashdateUserId.Create("fp:anna").Value,
            Source,
            kind,
            Profile.Create(new ProfileInput(
                ProfileName.Create("Анна").Value, Maybe<Age>.None, Maybe<City>.None, Maybe<Bio>.None,
                Interests.Empty, Maybe<PhotoFileId>.None, Maybe<ContactUsername>.None, "Анна")).Value,
            Now)).Value;
}
