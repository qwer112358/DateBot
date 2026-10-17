using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Errors;
using DateBot.Application.MiniApp;
using DateBot.Application.MiniApp.Enums;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.MiniApp.Options;
using DateBot.Application.Notifications;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Processing.Options;
using DateBot.Application.Telegram;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Matches.Enums;
using static DateBot.Application.Tests.Support.Samples;

namespace DateBot.Application.Tests.UseCases;

/// <summary>
/// Likes answered through the app, and the owner choosing where the first message goes: Telegram or the in-app chat.
/// </summary>
public sealed class MiniAppTests
{
    [Fact]
    public async Task Like_in_the_app_is_liked_back_and_the_draft_offers_both_channels()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола", username: "lola_tash");

        var report = await host.SyncAppAsync();

        report.Value.Drafted.Should().Be(1);
        host.App.Liked.Should().Equal(501);
        host.Client.Presses.Should().BeEmpty("the like goes through the app, not a bot button");
        var match = host.Store.SingleMatch;
        match.IsMutual.Should().BeTrue();
        match.Profile.Username.GetValueOrThrow().Value.Should().Be("lola_tash");
        match.Profile.AppId.GetValueOrThrow().Value.Should().Be(501);
        var draft = host.Notifier.Sent.OfType<DraftReadyNotification>().Single();
        draft.Mutual.Should().BeTrue();
        draft.Channels.Should().Equal(MessageChannel.Telegram, MessageChannel.App);
        host.App.Sent.Should().BeEmpty();
        host.Client.Sends.Should().Be(0);
    }

    [Fact]
    public async Task The_prompt_gets_the_whole_app_profile_and_its_photo()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола", bio: "Обожаю джаз и рамен");

        await host.SyncAppAsync();

        var (prompt, photo) = host.Generator.Calls.Single();
        prompt.User.Should().Contain("Обожаю джаз и рамен").And.Contain("Кофе, Походы").And.Contain("Питомцы: кошка");
        photo.HasValue.Should().BeTrue();
        host.Photos.Photos.Should().ContainKey("app:p501.jpg");
    }

    [Fact]
    public async Task Owner_picks_the_in_app_chat()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола", username: "lola_tash");
        await host.SyncAppAsync();
        var match = host.Store.SingleMatch;

        var decision = await host.DecideAsync(match.Id.Value, approve: true, MessageChannel.App);

        decision.Value.Should().Be(DraftDecision.Sent);
        var sent = host.App.Sent.Single();
        sent.To.Should().Be(501);
        sent.ClientKey.Should().Be(match.Id.Value.ToString("D"), "a repeat of the same message must be recognisable by the app");
        host.Client.Sends.Should().Be(0);
        match.Status.Should().Be(MatchStatus.Done);
        host.Notifier.Sent.OfType<LikeHandledNotification>().Single().Channel.Should().Be(MessageChannel.App);
    }

    [Fact]
    public async Task Owner_picks_Telegram()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола", username: "lola_tash");
        await host.SyncAppAsync();

        await host.DecideAsync(host.Store.SingleMatch.Id.Value, approve: true, MessageChannel.Telegram);

        host.Client.SentToUsers.Should().ContainSingle().Which.Username.Should().Be("lola_tash");
        host.App.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_a_username_only_the_in_app_chat_is_offered()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");

        await host.SyncAppAsync();

        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().Equal(MessageChannel.App);
        var telegram = await host.DecideAsync(host.Store.SingleMatch.Id.Value, approve: true, MessageChannel.Telegram);
        telegram.Error.Code.Should().Be("match.channel_unavailable");
    }

    [Fact]
    public async Task Telegram_refusal_brings_the_draft_back_to_pick_the_app()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола", username: "lola_tash");
        await host.SyncAppAsync();
        var match = host.Store.SingleMatch;
        host.Client.SendFailures.Enqueue(TelegramErrors.Rpc(400, "USER_PRIVACY_RESTRICTED"));

        var refused = await host.DecideAsync(match.Id.Value, approve: true, MessageChannel.Telegram);

        refused.IsFailure.Should().BeTrue();
        match.Status.Should().NotBe(MatchStatus.Failed);
        match.DraftMessage.HasValue.Should().BeTrue();
        host.Notifier.Sent.OfType<DraftReadyNotification>().Should().HaveCount(2);

        (await host.DecideAsync(match.Id.Value, approve: true, MessageChannel.App)).Value.Should().Be(DraftDecision.Sent);
        host.App.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task In_app_send_with_unknown_outcome_is_not_repeated()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");
        await host.SyncAppAsync();
        var match = host.Store.SingleMatch;
        host.App.SendFailures.Enqueue(AppErrors.SendOutcomeUnknown("HTTP 502"));

        await host.DecideAsync(match.Id.Value, approve: true, MessageChannel.App);

        match.Status.Should().Be(MatchStatus.Failed);
        host.Notifier.Sent.OfType<ActionFailedNotification>().Single().Reason.Code.Should().Be(DeliveryErrors.OutcomeUnknownCode);
        (await host.DecideAsync(match.Id.Value, approve: true, MessageChannel.App)).IsFailure.Should().BeTrue();
        host.App.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Match_card_after_the_like_back_is_the_same_person()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Аня", username: "anya_sample");
        await host.SyncAppAsync();

        var card = await host.ProcessAsync(TashdateMatch(40));

        card.Value.Should().Be(ProcessOutcome.Ignored);
        host.Store.Matches.Should().ContainSingle();
        host.Notifier.Sent.OfType<DraftReadyNotification>().Should().ContainSingle();
    }

    [Fact]
    public async Task Match_card_for_someone_liked_in_the_feed_is_completed_from_the_app()
    {
        await using var host = Tashdate();
        host.App.Matches.Add(new AppMatch(777, "Аня", Maybe.From("anya_sample")));
        host.App.AddLike(777, "Аня", bio: "Учусь на архитектора, люблю старые дворики");
        host.App.Likes.Clear();

        var card = await host.ProcessAsync(TashdateMatch(40));

        card.Value.Should().Be(ProcessOutcome.AwaitingConfirmation);
        var match = host.Store.SingleMatch;
        match.Profile.AppId.GetValueOrThrow().Value.Should().Be(777);
        match.Profile.Bio.GetValueOrThrow().Value.Should().Contain("архитектора");
        host.Generator.Calls.Single().Prompt.User.Should().Contain("архитектора");
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().Equal(MessageChannel.Telegram, MessageChannel.App);
    }

    [Fact]
    public async Task Card_without_an_app_match_offers_Telegram_only()
    {
        await using var host = Tashdate();

        await host.ProcessAsync(TashdateMatch(40));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().Equal(MessageChannel.Telegram);
    }

    [Fact]
    public async Task With_the_app_switched_off_the_in_app_chat_is_never_offered()
    {
        await using var host = Tashdate(miniApp: false);
        host.App.Matches.Add(new AppMatch(777, "Аня", Maybe.From("anya_sample")));

        await host.ProcessAsync(TashdateMatch(40));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().Equal(MessageChannel.Telegram);
        host.Store.SingleMatch.Profile.AppId.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public async Task Hidden_likes_are_counted_but_not_answered()
    {
        await using var host = Tashdate();
        host.App.HiddenLikes = 3;

        var report = await host.SyncAppAsync();

        report.Value.Should().Be(new AppSyncReport(Open: 0, Hidden: 3, Done: 0, Drafted: 0, Ignored: 0, Failed: 0));
        host.App.Liked.Should().BeEmpty();
    }

    [Fact]
    public async Task Known_person_is_not_loaded_again()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");
        host.App.LikeFailures.Enqueue(AppErrors.Refused(400, "already_liked"));
        await host.SyncAppAsync();
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Failed);

        var again = await host.SyncAppAsync();

        again.Value.Ignored.Should().Be(1);
        host.App.ProfileReads.Should().Be(1);
        host.App.Liked.Should().BeEmpty();
    }

    [Fact]
    public async Task Declined_by_the_filters_is_kept_and_never_liked()
    {
        var processing = TestOptions.TashdateProcessing();
        await using var host = new TestHost(
            processing: new ProcessingOptions
            {
                Mode = processing.Mode,
                Channels = processing.Channels,
                MiniApp = processing.MiniApp,
                LikeBackButtons = processing.LikeBackButtons,
                Filters = new FilterOptions { Enabled = true, MinAge = 25, MaxAge = 40 },
            },
            parsing: TestOptions.TashdateParsing());
        host.App.AddLike(501, "Лола");

        await host.SyncAppAsync();
        await host.SyncAppAsync();

        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Skipped);
        host.App.Liked.Should().BeEmpty();
        host.App.ProfileReads.Should().Be(1);
    }

    [Fact]
    public async Task Daily_like_limit_stops_the_pass_and_the_like_is_repeated_later()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");
        host.App.AddLike(502, "Мадина");
        host.App.LikeFailures.Enqueue(AppErrors.LikeLimit(30, TimeSpan.FromHours(1)));

        var limited = await host.SyncAppAsync();

        limited.Error.Code.Should().Be(AppErrors.LikeLimitCode);
        ErrorClassifier.IsRetryable(limited.Error).Should().BeTrue();
        host.Store.SingleMatch.PendingAction.HasValue.Should().BeTrue();
        host.App.ProfileReads.Should().Be(1, "the pass stops before the second like");

        var later = await host.SyncAppAsync();

        later.Value.Drafted.Should().Be(2);
        host.App.Liked.Should().Equal(501, 502);
    }

    [Fact]
    public async Task Like_left_pending_is_found_among_the_matches_instead_of_liking_again()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола", username: "lola_tash");
        host.App.LikeFailures.Enqueue(AppErrors.Unavailable("timeout"));
        await host.SyncAppAsync();
        host.App.Matches.Add(new AppMatch(501, "Лола", Maybe.From("lola_tash")));

        await host.SyncAppAsync();

        host.App.Liked.Should().BeEmpty("the first like went through before the connection broke");
        host.Store.SingleMatch.IsMutual.Should().BeTrue();
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Channels.Should().Contain(MessageChannel.Telegram);
    }

    [Fact]
    public async Task At_most_the_configured_number_of_likes_per_pass()
    {
        var processing = TestOptions.TashdateProcessing();
        await using var host = new TestHost(
            processing: new ProcessingOptions
            {
                Mode = processing.Mode,
                Channels = processing.Channels,
                LikeBackButtons = processing.LikeBackButtons,
                MiniApp = new MiniAppOptions { LikeBack = LikeBackMode.Auto, MaxLikesPerPass = 2 },
            },
            parsing: TestOptions.TashdateParsing());
        foreach (var id in new long[] { 501, 502, 503 })
        {
            host.App.AddLike(id, $"Анкета{id}");
        }

        var first = await host.SyncAppAsync();

        first.Value.Answered.Should().Be(2);
        first.Value.Deferred.Should().Be(1);
        host.App.Liked.Should().HaveCount(2);
    }

    [Fact]
    public async Task Forgotten_person_is_not_answered_again_from_the_app()
    {
        await using var host = Tashdate();
        host.App.AddLike(501, "Лола");
        await host.SyncAppAsync();
        await host.OwnerAsync(new ForgetRequest(7, 42, host.Store.SingleMatch.Id.Value));
        host.App.AddLike(501, "Лола");

        var again = await host.SyncAppAsync();

        again.Value.Ignored.Should().Be(1);
        host.Store.Matches.Should().BeEmpty();
        host.App.Liked.Should().ContainSingle();
    }

    [Fact]
    public async Task Paused_service_does_not_touch_the_app()
    {
        await using var host = Tashdate();
        host.Store.Control = host.Store.Control with { Paused = true };
        host.App.AddLike(501, "Лола");

        var paused = await host.SyncAppAsync();

        paused.Error.Code.Should().Be(ServiceErrors.Paused.Code);
        host.App.ProfileReads.Should().Be(0);
    }

    [Fact]
    public async Task Auto_mode_sends_to_the_preferred_channel()
    {
        await using var host = Tashdate(mode: ProcessingMode.Auto);
        host.App.AddLike(501, "Лола");

        await host.SyncAppAsync();

        host.App.Sent.Should().ContainSingle("without a username the in-app chat is the only channel");
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Done);
    }

    private static TestHost Tashdate(bool miniApp = true, ProcessingMode mode = ProcessingMode.Confirm) =>
        new(processing: TestOptions.TashdateProcessing(miniApp, mode), parsing: TestOptions.TashdateParsing());
}
