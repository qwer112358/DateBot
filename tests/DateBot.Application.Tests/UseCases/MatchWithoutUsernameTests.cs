using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.Notifications;
using DateBot.Application.Parsing.BotNotifications;
using DateBot.Application.Parsing.Models;
using DateBot.Application.Parsing.Services;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Matches.Enums;
using static DateBot.Application.Tests.Support.Samples;

namespace DateBot.Application.Tests.UseCases;

/// <summary>
/// A match card can come without @username (the person has none): only the name and the photo. Telegram is then out,
/// the app is the only channel, and the owner must hear about the match even when the app cannot find the person.
/// </summary>
public sealed class MatchWithoutUsernameTests
{
    private static readonly ParsingRules Rules = ParsingRules.From(TestOptions.TashdateParsing());

    [Fact]
    public void Same_name_with_another_photo_is_another_person()
    {
        var first = (MutualNotification)BotNotificationParser.Parse(TashdateMatchWithoutUsername(40, "Шахноза", "photo:1"), Rules);
        var second = (MutualNotification)BotNotificationParser.Parse(TashdateMatchWithoutUsername(41, "Шахноза", "photo:2"), Rules);

        first.Profile.Username.HasNoValue.Should().BeTrue();
        ProfileMapper.PersonKey(first.Profile).Should().NotBe(ProfileMapper.PersonKey(second.Profile));
    }

    [Fact]
    public async Task Two_people_with_the_same_name_get_two_drafts()
    {
        await using var host = Tashdate();

        await host.ProcessAsync(TashdateMatchWithoutUsername(40, "Шахноза", "photo:1"));
        var second = await host.ProcessAsync(TashdateMatchWithoutUsername(41, "Шахноза", "photo:2"));

        second.Value.Should().Be(ProcessOutcome.AwaitingConfirmation);
        host.Store.Matches.Should().HaveCount(2);
    }

    [Fact]
    public async Task Found_in_the_app_by_name_among_matches_without_username()
    {
        await using var host = Tashdate();
        host.App.Matches.Add(new AppMatch(601, "Шахноза", Maybe.From("sh_other")));
        host.App.Matches.Add(new AppMatch(602, "Шахноза", Maybe<string>.None));
        host.App.AddLike(602, "Шахноза", bio: "Рисую акварелью");
        host.App.Likes.Clear();

        await host.ProcessAsync(TashdateMatchWithoutUsername(40));

        var match = host.Store.SingleMatch;
        match.Profile.AppId.GetValueOrThrow().Value.Should().Be(602, "the namesake with a username is someone else");
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().Equal(MessageChannel.App);
        host.Generator.Calls.Single().Prompt.User.Should().Contain("акварелью");
    }

    [Fact]
    public async Task Two_namesakes_without_username_are_not_guessed_between()
    {
        await using var host = Tashdate();
        host.App.Matches.Add(new AppMatch(601, "Шахноза", Maybe<string>.None));
        host.App.Matches.Add(new AppMatch(602, "Шахноза", Maybe<string>.None));

        await host.ProcessAsync(TashdateMatchWithoutUsername(40));

        host.Store.SingleMatch.Profile.AppId.HasNoValue.Should().BeTrue();
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcessingMode.Confirm)]
    [InlineData(ProcessingMode.Auto)]
    public async Task Match_no_channel_reaches_still_brings_the_text_to_the_owner(ProcessingMode mode)
    {
        await using var host = Tashdate(mode);

        var outcome = await host.ProcessAsync(TashdateMatchWithoutUsername(40));

        outcome.Value.Should().Be(ProcessOutcome.AwaitingConfirmation);
        var draft = host.Notifier.Sent.OfType<DraftReadyNotification>().Single();
        draft.Channels.Should().BeEmpty();
        draft.Mutual.Should().BeTrue();
        host.Client.Sends.Should().Be(0);
        host.App.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task App_failure_does_not_hold_the_match_back()
    {
        await using var host = Tashdate();
        host.App.MatchesFailure = AppErrors.Unavailable("timeout");

        var outcome = await host.ProcessAsync(TashdateMatchWithoutUsername(40));

        outcome.Value.Should().Be(ProcessOutcome.AwaitingConfirmation);
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().BeEmpty();
    }

    [Fact]
    public async Task Find_in_the_app_later_brings_the_card_with_the_app_channel()
    {
        await using var host = Tashdate();
        await host.ProcessAsync(TashdateMatchWithoutUsername(40));
        var match = host.Store.SingleMatch;
        host.App.Matches.Add(new AppMatch(602, "Шахноза", Maybe<string>.None));
        host.App.AddLike(602, "Шахноза");
        host.App.Likes.Clear();

        await host.OwnerAsync(new LookUpContactsRequest(7, 42, match.Id.Value));

        match.Profile.AppId.GetValueOrThrow().Value.Should().Be(602);
        host.Notifier.Sent.OfType<DraftReadyNotification>().Last().Channels.Should().Equal(MessageChannel.App);
        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().StartWith("Нашёл");

        (await host.DecideAsync(match.Id.Value, approve: true, MessageChannel.App)).Value.Should().Be(DraftDecision.Sent);
        host.App.Sent.Single().To.Should().Be(602);
    }

    [Fact]
    public async Task Find_in_the_app_that_finds_nobody_says_so()
    {
        await using var host = Tashdate();
        await host.ProcessAsync(TashdateMatchWithoutUsername(40));

        await host.OwnerAsync(new LookUpContactsRequest(7, 42, host.Store.SingleMatch.Id.Value));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Should().ContainSingle();
        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().Contain("не найден");
    }

    private static TestHost Tashdate(ProcessingMode mode = ProcessingMode.Confirm) =>
        new(processing: TestOptions.TashdateProcessing(mode: mode), parsing: TestOptions.TashdateParsing());
}
