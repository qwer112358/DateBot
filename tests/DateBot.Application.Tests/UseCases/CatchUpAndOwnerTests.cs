using DateBot.Application.Notifications;
using DateBot.Application.Telegram;
using DateBot.Application.Telegram.Models;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Inbox.Enums;
using static DateBot.Application.Tests.Support.Samples;

namespace DateBot.Application.Tests.UseCases;

public sealed class CatchUpAndOwnerTests
{
    private const string MadinaProfile = "Мадина, 23, Бухара – Рисую и катаюсь на велосипеде";

    [Fact]
    public async Task Catch_up_reads_everything_after_the_cursor_in_order()
    {
        await using var host = new TestHost();
        host.Client.History.AddRange([Mutual(12, MadinaProfile, "madina_b"), Like(10), Text(11, "Новости бота")]);

        var report = await host.CatchUpAsync();

        report.Value.Should().Be(new CatchUpReport(Processed: 3, Done: 2, Drafted: 0, Ignored: 0, ParseFailed: 1, Failed: 0));
        host.Store.Cursor.Position.Should().Be(12);
        host.Store.Inbox.Keys.Should().BeEquivalentTo([10L, 11L, 12L]);
    }

    [Fact]
    public async Task First_run_does_not_answer_old_likes_from_the_chat_history()
    {
        await using var host = new TestHost(TestOptions.Processing(firstRunBackfill: TimeSpan.Zero));
        host.Client.History.AddRange([Like(10), Mutual(12, MadinaProfile, "madina_b")]);

        var first = await host.CatchUpAsync();
        host.Client.History.Add(Like(15, "Нигора, 26, Ташкент – Танцы и кино"));
        var next = await host.CatchUpAsync();

        first.Value.Processed.Should().Be(0);
        next.Value.Processed.Should().Be(1);
        host.Client.Presses.Should().OnlyContain(p => p.MessageId == 15);
        host.Store.Cursor.Position.Should().Be(15);
    }

    [Fact]
    public async Task Catch_up_after_a_restart_continues_from_the_cursor()
    {
        await using var host = new TestHost();
        host.Client.History.Add(Like(10));
        await host.CatchUpAsync();
        host.Client.History.Add(Mutual(12, MadinaProfile, "madina_b"));

        var report = await host.CatchUpAsync();

        report.Value.Processed.Should().Be(1);
        host.Client.Sends.Should().Be(2);
    }

    [Fact]
    public async Task Catch_up_stops_on_flood_wait_and_resumes_without_duplicates()
    {
        await using var host = new TestHost();
        host.Client.History.AddRange([Like(10), Mutual(12, MadinaProfile, "madina_b")]);
        host.Client.SendFailures.Enqueue(TelegramErrors.FloodWait(60));

        var first = await host.CatchUpAsync();
        var second = await host.CatchUpAsync();

        first.IsFailure.Should().BeTrue();
        second.Value.Processed.Should().Be(2);
        host.Client.Presses.Count(p => p.Labels.Contains("❤")).Should().Be(1);
        host.Client.Sends.Should().Be(2);
        host.Store.Inbox.Values.Should().OnlyContain(m => m.Status == InboxStatus.Done);
    }

    [Fact]
    public async Task Catch_up_does_nothing_while_paused()
    {
        await using var host = new TestHost();
        host.Client.History.Add(Like(10));
        host.Store.Control = host.Store.Control with { Paused = true };

        var report = await host.CatchUpAsync();

        report.Error.Code.Should().Be("service.paused");
        host.Store.Inbox.Should().BeEmpty();
    }

    [Fact]
    public async Task Stop_and_start_commands_toggle_the_pause()
    {
        await using var host = new TestHost();

        await host.OwnerAsync(new StopRequest(7, 42));
        host.Store.Control.Paused.Should().BeTrue();

        await host.OwnerAsync(new StartRequest(7, 42));
        host.Store.Control.Paused.Should().BeFalse();
        host.Notifier.Sent.OfType<TextNotification>().Select(n => n.Text).Should().Contain(t => t.Contains("возобновлена"));
    }

    [Fact]
    public async Task First_start_remembers_the_chat()
    {
        await using var host = new TestHost();
        host.Store.Control = host.Store.Control with { OwnerChatId = CSharpFunctionalExtensions.Maybe<long>.None };

        await host.OwnerAsync(new StartRequest(555, 42));

        host.Store.Control.OwnerChatId.Should().Be(CSharpFunctionalExtensions.Maybe.From(555L));
    }

    [Fact]
    public async Task Strangers_are_ignored()
    {
        await using var host = new TestHost();

        var result = await host.OwnerAsync(new StopRequest(7, 999));

        result.Error.Should().Be(OwnerErrors.Unauthorized);
        host.Store.Control.Paused.Should().BeFalse();
        host.Notifier.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Status_reports_counts_and_cursor()
    {
        await using var host = new TestHost();
        await host.ProcessAsync(Like(10));

        await host.OwnerAsync(new StatusRequest(7, 42));

        var status = host.Notifier.Sent.OfType<StatusNotification>().Should().ContainSingle().Subject.Report;
        status.Cursor.Should().Be(10);
        status.Inbox[InboxStatus.Done].Should().Be(1);
    }

    [Fact]
    public async Task Forget_removes_everything_about_the_person()
    {
        await using var host = new TestHost();
        host.Client.Photo = CSharpFunctionalExtensions.Maybe.From(new ProfilePhoto(new byte[] { 1 }, "image/jpeg"));
        await host.ProcessAsync(Like(10));
        var matchId = host.Store.SingleMatch.Id.Value;

        await host.OwnerAsync(new ForgetRequest(7, 42, matchId));

        host.Store.Matches.Should().BeEmpty();
        host.Store.Inbox.Should().NotContainKey(10);
        host.Photos.Photos.Should().BeEmpty();
        host.Store.Cursor.Position.Should().Be(10, "the cursor stays so the message is not processed again");
    }
}
