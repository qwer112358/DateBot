using DateBot.Application.MiniApp.Enums;
using DateBot.Application.MiniApp.Options;
using DateBot.Application.Notifications;
using DateBot.Application.Processing.Options;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.Tests.UseCases;

/// <summary>
/// The shipped <see cref="LikeBackMode.Confirm"/>: a new like in the app comes to you as a card, and the like back
/// happens only after your «❤️ Ответить». /likes checks the app at once.
/// </summary>
public sealed class LikeCardTests
{
    [Fact]
    public async Task New_like_comes_as_a_card_with_the_photo_and_is_not_liked()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");

        var report = await host.SyncAppAsync();

        report.Value.Proposed.Should().Be(1);
        report.Value.Answered.Should().Be(0);
        host.App.Liked.Should().BeEmpty();
        var card = host.Notifier.Sent.Should().ContainSingle().Which.Should().BeOfType<LikeBackProposedNotification>().Subject;
        card.Profile.Name.Value.Should().Be("Лола");
        card.Profile.Photo.HasValue.Should().BeTrue();
        host.Photos.Photos.Should().ContainKey("app:p501.jpg", "the card shows the photo, so it is stored before the card goes out");
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Received);
    }

    [Fact]
    public async Task Card_goes_out_once_and_waits()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");
        await host.SyncAppAsync();

        var again = await host.SyncAppAsync();

        again.Value.Waiting.Should().Be(1);
        again.Value.Proposed.Should().Be(0);
        host.Notifier.Sent.OfType<LikeBackProposedNotification>().Should().ContainSingle();
        host.App.ProfileReads.Should().Be(1);
        host.App.Liked.Should().BeEmpty();
    }

    [Fact]
    public async Task Yes_wakes_the_loop_whose_next_pass_likes_back_and_drafts_the_message()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола", username: "lola_tash");
        await host.SyncAppAsync();
        var match = host.Store.SingleMatch;

        await host.OwnerAsync(new LikeBackDecisionRequest(7, 42, match.Id.Value, Approve: true));

        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().StartWith("Принято");
        host.AppCheck.Take().Should().Be(AppCheckRequest.Wake);
        host.App.Liked.Should().BeEmpty("the main loop does the like, not the button handler");

        var pass = await host.SyncAppAsync();

        pass.Value.Drafted.Should().Be(1);
        host.App.Liked.Should().Equal(501);
        match.Status.Should().Be(MatchStatus.LikedBack);
        match.Actions.Should().ContainSingle(a => a.Type == ActionType.LikeBack, "the approved card is the like back itself");
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().Equal(MessageChannel.Telegram, MessageChannel.App);
    }

    [Fact]
    public async Task No_closes_the_person_and_nothing_is_liked()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");
        await host.SyncAppAsync();
        var match = host.Store.SingleMatch;

        await host.OwnerAsync(new LikeBackDecisionRequest(7, 42, match.Id.Value, Approve: false));
        var later = await host.SyncAppAsync();

        match.Status.Should().Be(MatchStatus.Skipped);
        host.AppCheck.Take().Should().Be(AppCheckRequest.None);
        later.Value.Ignored.Should().Be(1);
        host.App.Liked.Should().BeEmpty();
        host.Notifier.Sent.OfType<LikeBackProposedNotification>().Should().ContainSingle();
    }

    [Fact]
    public async Task Second_press_on_the_same_card_is_refused()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");
        await host.SyncAppAsync();
        var id = host.Store.SingleMatch.Id.Value;
        await host.OwnerAsync(new LikeBackDecisionRequest(7, 42, id, Approve: true));

        await host.OwnerAsync(new LikeBackDecisionRequest(7, 42, id, Approve: false));

        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().Contain("решение уже принято");
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Received, "the earlier yes still stands");
    }

    [Fact]
    public async Task Cards_count_against_the_per_pass_limit()
    {
        var processing = TestOptions.TashdateProcessing(likeBack: LikeBackMode.Confirm);
        await using var host = new TestHost(
            processing: new ProcessingOptions
            {
                Mode = processing.Mode,
                Channels = processing.Channels,
                LikeBackButtons = processing.LikeBackButtons,
                MiniApp = new MiniAppOptions { LikeBack = LikeBackMode.Confirm, MaxLikesPerPass = 2 },
            },
            parsing: TestOptions.TashdateParsing());
        foreach (var id in new long[] { 501, 502, 503 })
        {
            host.App.AddLike(id, $"Анкета{id}");
        }

        var first = await host.SyncAppAsync();
        var second = await host.SyncAppAsync();

        first.Value.Proposed.Should().Be(2);
        first.Value.Deferred.Should().Be(1);
        second.Value.Waiting.Should().Be(2);
        second.Value.Proposed.Should().Be(1);
        host.Notifier.Sent.OfType<LikeBackProposedNotification>().Should().HaveCount(3);
    }

    [Fact]
    public async Task Likes_command_asks_the_loop_for_a_check_with_a_summary()
    {
        await using var host = Tashdate();

        await host.OwnerAsync(new CheckLikesRequest(7, 42));

        host.AppCheck.Take().Should().Be(AppCheckRequest.Report);
        host.Notifier.Sent.OfType<TextNotification>().Single().Text.Should().Contain("Проверяю лайки");
    }

    [Fact]
    public async Task Likes_command_with_the_app_switched_off_only_says_so()
    {
        await using var host = Tashdate(miniApp: false);

        await host.OwnerAsync(new CheckLikesRequest(7, 42));

        host.AppCheck.Take().Should().Be(AppCheckRequest.None);
        host.Notifier.Sent.OfType<TextNotification>().Single().Text.Should().Contain("Mini App выключена");
    }

    [Fact]
    public async Task Likes_command_while_stopped_only_says_so()
    {
        await using var host = Tashdate();
        host.Store.Control = host.Store.Control with { Paused = true };

        await host.OwnerAsync(new CheckLikesRequest(7, 42));

        host.AppCheck.Take().Should().Be(AppCheckRequest.None);
        host.Notifier.Sent.OfType<TextNotification>().Single().Text.Should().Contain("остановлен");
    }

    private static TestHost Tashdate(bool miniApp = true) =>
        new(processing: TestOptions.TashdateProcessing(miniApp, likeBack: LikeBackMode.Confirm), parsing: TestOptions.TashdateParsing());
}
