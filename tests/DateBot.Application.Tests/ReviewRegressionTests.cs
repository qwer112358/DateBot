using DateBot.Application.Notifications;
using DateBot.Application.Processing;
using DateBot.Application.Telegram;
using DateBot.Application.UseCases;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;
using static DateBot.Application.Tests.Samples;

namespace DateBot.Application.Tests;

/// <summary>Failure paths found in the review of the critical flows.</summary>
public sealed class ReviewRegressionTests
{
    [Fact]
    public async Task Send_with_unknown_outcome_is_never_repeated()
    {
        await using var host = new TestHost();
        host.Client.SendFailures.Enqueue(TelegramErrors.SendOutcomeUnknown("connection reset"));

        var first = await host.ProcessAsync(Mutual(11));
        var again = await host.ProcessAsync(Mutual(11));

        first.Value.Should().Be(ProcessOutcome.Failed);
        again.Value.Should().Be(ProcessOutcome.AlreadyProcessed);
        host.Client.Sends.Should().Be(0);
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Failed);
        host.Notifier.Sent.OfType<ActionFailedNotification>().Single().Reason.Code.Should().Be(TelegramErrors.SendOutcomeUnknownCode);
    }

    [Fact]
    public async Task Interrupted_like_whose_button_is_gone_counts_as_done()
    {
        await using var host = new TestHost();
        host.Client.PressFailures.Enqueue(TelegramErrors.Unavailable("connection reset"));
        host.Client.PressFailures.Enqueue(TelegramErrors.ButtonNotFound(BotMessageId.Create(10).Value, ["❤"]));

        var first = await host.ProcessAsync(Like(10));
        var retry = await host.ProcessAsync(Like(10));

        first.IsFailure.Should().BeTrue();
        retry.Value.Should().Be(ProcessOutcome.Done);
        host.Store.SingleMatch.HasLikedBack.Should().BeTrue();
        host.Client.Sends.Should().Be(1);
    }

    [Fact]
    public async Task Draft_approved_during_a_pause_is_offered_again()
    {
        await using var host = new TestHost(TestOptions.Processing(ProcessingMode.Confirm));
        await host.ProcessAsync(Like(10));
        var matchId = host.Store.SingleMatch.Id.Value;
        host.Store.Control = host.Store.Control with { Paused = true };

        var paused = await host.DecideAsync(matchId, approve: true);
        host.Store.Control = host.Store.Control with { Paused = false };
        var resumed = await host.DecideAsync(matchId, approve: true);

        paused.Error.Code.Should().Be("service.paused");
        host.Notifier.Sent.OfType<DraftReadyNotification>().Should().HaveCount(2, "the card with buttons comes back");
        resumed.Value.Should().Be(DraftDecision.Sent);
        host.Client.Sends.Should().Be(1);
    }

    [Fact]
    public async Task Approved_draft_is_sent_against_the_notification_it_answers()
    {
        await using var host = new TestHost(TestOptions.Processing(ProcessingMode.Confirm, LikedMeMessagePolicy.OnMutual));
        await host.ProcessAsync(Like(10));
        await host.ProcessAsync(Text(11, $"Есть взаимная симпатия!\n\n{AnnaProfile}"));

        var decision = await host.DecideAsync(host.Store.SingleMatch.Id.Value, approve: true);

        decision.Value.Should().Be(DraftDecision.Sent);
        host.Client.Presses.Last().MessageId.Should().Be(11, "the mutual notification, not the like card 10");
        host.Client.Presses.Last().Labels.Should().Equal("💌");
        host.Client.SentToBot.Should().ContainSingle();
    }

    [Fact]
    public async Task Plain_text_is_not_sent_when_the_bot_already_shows_another_profile()
    {
        await using var host = new TestHost(TestOptions.Processing(messageButtons: []));
        host.Client.LatestMessageId = CSharpFunctionalExtensions.Maybe.From(99L);

        var outcome = await host.ProcessAsync(Like(10));

        outcome.Value.Should().Be(ProcessOutcome.Failed);
        host.Client.SentToBot.Should().BeEmpty();
        host.Notifier.Sent.OfType<ActionFailedNotification>().Single().Reason.Code.Should().Be("telegram.stale_anchor");
    }

    [Fact]
    public async Task Forgotten_person_is_not_answered_again()
    {
        await using var host = new TestHost();
        await host.ProcessAsync(Like(10));
        await host.OwnerAsync(new ForgetRequest(7, 42, host.Store.SingleMatch.Id.Value));

        var later = await host.ProcessAsync(Mutual(11));

        later.Value.Should().Be(ProcessOutcome.Ignored);
        host.Store.Matches.Should().BeEmpty();
        host.Client.Sends.Should().Be(1);
    }

    [Fact]
    public async Task First_run_backfill_reads_only_the_window_and_finishes()
    {
        await using var host = new TestHost(TestOptions.Processing(firstRunBackfill: TimeSpan.FromDays(1)));
        host.Client.History.AddRange(
        [
            Like(5, "Ольга, 30, Ташкент – Старая анкета") with { SentAt = SentAt.AddDays(-3) },
            Like(10),
            Mutual(12, "Мадина, 23, Бухара – Рисую и катаюсь на велосипеде", "madina_b"),
        ]);

        var report = await host.CatchUpAsync();

        report.Value.Processed.Should().Be(2);
        host.Store.Inbox.Keys.Should().BeEquivalentTo([10L, 12L]);
        host.Store.Cursor.Position.Should().Be(12);
    }
}
