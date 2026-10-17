using CSharpFunctionalExtensions;
using TashdateBot.Application.Notifications;
using TashdateBot.Application.Parsing;
using TashdateBot.Application.Processing;
using TashdateBot.Application.Telegram;
using TashdateBot.Application.UseCases;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Inbox;
using TashdateBot.Domain.Matches;
using TashdateBot.Domain.Shared;
using static TashdateBot.Application.Tests.Samples;

namespace TashdateBot.Application.Tests;

public sealed class ProcessBotMessageTests
{
    private static readonly ProfilePhoto Jpeg = new(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg");

    [Fact]
    public async Task Scenario_A_likes_back_and_sends_a_message_with_photo()
    {
        await using var host = new TestHost();
        host.Client.Photo = Maybe.From(Jpeg);

        var outcome = await host.ProcessAsync(Like(10));

        outcome.Value.Should().Be(ProcessOutcome.Done);
        host.Client.Presses.Select(p => (p.MessageId, p.Labels.Single())).Should().Equal((10L, "❤"), (10L, "💌"));
        host.Client.SentToBot.Should().Equal(FakeReplyGenerator.GoodReply);
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Done);
        host.Store.Inbox[10].Status.Should().Be(InboxStatus.Done);
        host.Store.Cursor.Position.Should().Be(10);
        host.Generator.Calls.Single().Photo.HasValue.Should().BeTrue();
        host.Photos.Photos.Should().ContainKey("photo:111");
        host.Notifier.Sent.OfType<LikeHandledNotification>().Should().ContainSingle().Which.LikedBack.Should().BeTrue();
        host.Clock.Delays.Should().Contain([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3)]);
    }

    [Fact]
    public async Task Scenario_B_writes_directly_by_username_without_like()
    {
        await using var host = new TestHost();

        var outcome = await host.ProcessAsync(Mutual(11));

        outcome.Value.Should().Be(ProcessOutcome.Done);
        host.Client.Presses.Should().BeEmpty();
        host.Client.SentToUsers.Should().Equal(("anna_tash", FakeReplyGenerator.GoodReply));
        host.Notifier.Sent.OfType<MutualHandledNotification>().Should().ContainSingle();
    }

    [Fact]
    public async Task One_auto_message_per_person_even_after_mutual_like()
    {
        await using var host = new TestHost();

        await host.ProcessAsync(Like(10));
        var second = await host.ProcessAsync(Mutual(11));

        second.Value.Should().Be(ProcessOutcome.Ignored);
        host.Client.Sends.Should().Be(1);
        host.Store.Cursor.Position.Should().Be(11);
    }

    [Fact]
    public async Task Same_message_twice_does_nothing_the_second_time()
    {
        await using var host = new TestHost();

        await host.ProcessAsync(Like(10));
        var again = await host.ProcessAsync(Like(10));

        again.Value.Should().Be(ProcessOutcome.AlreadyProcessed);
        host.Client.Sends.Should().Be(1);
        host.Client.Presses.Should().HaveCount(2);
    }

    [Fact]
    public async Task On_mutual_policy_likes_back_first_and_writes_after_the_mutual_like()
    {
        await using var host = new TestHost(TestOptions.Processing(likedMe: LikedMeMessagePolicy.OnMutual));

        var like = await host.ProcessAsync(Like(10));
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.LikedBack);
        host.Client.Sends.Should().Be(0);
        host.Notifier.Sent.OfType<LikedBackNotification>().Should().ContainSingle();

        var mutual = await host.ProcessAsync(Mutual(11));

        like.Value.Should().Be(ProcessOutcome.Done);
        mutual.Value.Should().Be(ProcessOutcome.Done);
        host.Client.SentToUsers.Should().ContainSingle().Which.Username.Should().Be("anna_tash");
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Done);
    }

    [Fact]
    public async Task Unrecognised_message_goes_to_parse_failed_and_the_owner_is_told()
    {
        await using var host = new TestHost();

        var outcome = await host.ProcessAsync(Text(14, "Новая функция: суперлайки!"));

        outcome.Value.Should().Be(ProcessOutcome.ParseFailed);
        host.Store.Inbox[14].Status.Should().Be(InboxStatus.ParseFailed);
        host.Store.Cursor.Position.Should().Be(14);
        host.Notifier.Sent.OfType<ParseFailedNotification>().Should().ContainSingle()
            .Which.RawText.Should().Contain("суперлайки");
    }

    [Fact]
    public async Task Flood_wait_leaves_the_message_for_a_retry_and_sends_only_once()
    {
        await using var host = new TestHost();
        host.Client.SendFailures.Enqueue(TelegramErrors.FloodWait(30));

        var first = await host.ProcessAsync(Mutual(11));

        first.IsFailure.Should().BeTrue();
        first.Error.Type.Should().Be(ErrorType.RateLimited);
        first.Error.RetryAfter.Should().Be(Maybe.From(TimeSpan.FromSeconds(30)));
        host.Store.Cursor.Position.Should().Be(0);
        host.Store.Inbox[11].Status.Should().Be(InboxStatus.Received);

        var retry = await host.ProcessAsync(Mutual(11));

        retry.Value.Should().Be(ProcessOutcome.Done);
        host.Client.Sends.Should().Be(1);
        host.Store.SingleMatch.Actions.Select(a => a.Status).Should().Equal(ActionStatus.Failed, ActionStatus.Done);
    }

    [Fact]
    public async Task Hourly_limit_stops_before_any_action()
    {
        await using var host = new TestHost();
        host.Limiter.Failures.Enqueue(Error.RateLimited("service.hourly_limit", "limit", TimeSpan.FromMinutes(10)));

        var outcome = await host.ProcessAsync(Like(10));

        outcome.Error.Code.Should().Be("service.hourly_limit");
        host.Client.Presses.Should().BeEmpty();
        host.Store.Cursor.Position.Should().Be(0);
    }

    [Fact]
    public async Task Permanent_error_fails_the_match_and_moves_on()
    {
        await using var host = new TestHost();
        host.Client.PressFailures.Enqueue(TelegramErrors.ButtonNotFound(BotMessageId.Create(10).Value, ["❤"]));

        var outcome = await host.ProcessAsync(Like(10));

        outcome.Value.Should().Be(ProcessOutcome.Failed);
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Failed);
        host.Store.Inbox[10].Status.Should().Be(InboxStatus.Failed);
        host.Store.Cursor.Position.Should().Be(10);
        host.Notifier.Sent.OfType<ActionFailedNotification>().Should().ContainSingle();
    }

    [Fact]
    public async Task Confirm_mode_drafts_and_sends_after_approval()
    {
        await using var host = new TestHost(TestOptions.Processing(ProcessingMode.Confirm));

        var outcome = await host.ProcessAsync(Like(10));

        outcome.Value.Should().Be(ProcessOutcome.AwaitingConfirmation);
        host.Client.Presses.Should().ContainSingle("only the like back happens before approval");
        host.Client.Sends.Should().Be(0);
        var draft = host.Notifier.Sent.OfType<DraftReadyNotification>().Should().ContainSingle().Subject;
        draft.Draft.Value.Should().Be(FakeReplyGenerator.GoodReply);

        var decision = await host.DecideAsync(draft.MatchId.Value, approve: true);

        decision.Value.Should().Be(DraftDecision.Sent);
        host.Client.SentToBot.Should().Equal(FakeReplyGenerator.GoodReply);
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Done);
    }

    [Fact]
    public async Task Confirm_mode_skip_sends_nothing()
    {
        await using var host = new TestHost(TestOptions.Processing(ProcessingMode.Confirm));
        await host.ProcessAsync(Mutual(11));
        var draft = host.Notifier.Sent.OfType<DraftReadyNotification>().Single();

        var decision = await host.DecideAsync(draft.MatchId.Value, approve: false);

        decision.Value.Should().Be(DraftDecision.Skipped);
        host.Client.Sends.Should().Be(0);
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Skipped);
    }

    [Fact]
    public async Task Send_interrupted_by_a_restart_is_not_repeated()
    {
        await using var host = new TestHost();
        var message = Mutual(11);
        var mapped = ProfileMapper.ToDomain(((MutualNotification)BotNotificationParser.Parse(message, TestOptions.Rules())).Profile, message.Text).Value;
        var match = Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value, mapped.PersonId, BotMessageId.Create(11).Value, MatchKind.Mutual, mapped.Profile, host.Clock.UtcNow)).Value;
        match.StartMessage(ReplyText.Create(FakeReplyGenerator.GoodReply).Value, host.Clock.UtcNow);
        host.Store.Matches[match.Id.Value] = match;
        host.Store.Inbox[11] = InboxMessage.Receive(BotMessageId.Create(11).Value, message.Text, message.SentAt, host.Clock.UtcNow).Value;

        var outcome = await host.ProcessAsync(message);

        outcome.Value.Should().Be(ProcessOutcome.Failed);
        host.Client.Sends.Should().Be(0);
        host.Store.SingleMatch.Status.Should().Be(MatchStatus.Failed);
        host.Notifier.Sent.OfType<ActionFailedNotification>().Single().Reason.Should().Be(MatchErrors.SendOutcomeUnknown);
    }

    [Fact]
    public async Task Restart_after_like_back_sends_only_the_message()
    {
        await using var host = new TestHost();
        host.Client.SendFailures.Enqueue(TelegramErrors.Unavailable("connection reset"));

        var first = await host.ProcessAsync(Like(10));
        var second = await host.ProcessAsync(Like(10));

        first.IsFailure.Should().BeTrue();
        second.Value.Should().Be(ProcessOutcome.Done);
        host.Client.Presses.Count(p => p.Labels.Contains("❤")).Should().Be(1);
        host.Client.Sends.Should().Be(1);
    }

    [Fact]
    public async Task Stop_command_pauses_before_any_action_and_start_resumes()
    {
        await using var host = new TestHost();
        host.Store.Control = host.Store.Control with { Paused = true };

        var paused = await host.ProcessAsync(Like(10));
        host.Store.Control = host.Store.Control with { Paused = false };
        var resumed = await host.ProcessAsync(Like(10));

        paused.Error.Code.Should().Be("service.paused");
        resumed.Value.Should().Be(ProcessOutcome.Done);
        host.Client.Presses.Count(p => p.Labels.Contains("❤")).Should().Be(1);
    }

    [Fact]
    public async Task Invalid_command_is_rejected_by_the_validation_decorator()
    {
        await using var host = new TestHost();

        var outcome = await host.ProcessAsync(Text(0, "x"));

        outcome.Error.Type.Should().Be(ErrorType.Validation);
        host.Store.Inbox.Should().BeEmpty();
    }
}
