using DateBot.Application.Abstractions;
using DateBot.Application.Notifications;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Application.UseCases;
using DateBot.Domain.Common;
using Microsoft.Extensions.Options;

namespace DateBot.Worker;

/// <summary>
/// Main loop: log in, catch up on everything missed since the cursor, then wait for the bot to write
/// (or for the safety-net poll) and catch up again. Pauses on FLOOD_WAIT, the hourly limit and /stop.
/// </summary>
internal sealed class TashdateWorker(
    ITashdateClient client,
    ICommandDispatcher dispatcher,
    IOwnerNotifier notifier,
    IClock clock,
    IOptions<ProcessingOptions> options,
    ILogger<TashdateWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PausedRecheck = TimeSpan.FromSeconds(30);
    private readonly Backoff _backoff = new(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAsync(stoppingToken);

        var firstRun = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            var caughtUp = await dispatcher.SendAsync<CatchUpHistoryCommand, CatchUpReport>(new CatchUpHistoryCommand(), stoppingToken);
            if (caughtUp.IsSuccess)
            {
                _backoff.Reset();
                await ReportFirstRunAsync(firstRun, caughtUp.Value, stoppingToken);
                firstRun = false;
                await client.WaitForActivityAsync(options.Value.PollInterval, stoppingToken);
                continue;
            }

            await clock.DelayAsync(await DelayAfterAsync(caughtUp.Error, stoppingToken), stoppingToken);
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var connected = await client.ConnectAsync(ct);
            var remembered = connected.IsSuccess
                ? await dispatcher.SendAsync<RememberOwnerAccountCommand, Unit>(new RememberOwnerAccountCommand(connected.Value), ct)
                : CSharpFunctionalExtensions.Result.Failure<Unit, Error>(connected.Error);
            if (remembered.IsSuccess)
            {
                _backoff.Reset();
                return;
            }

            var delay = _backoff.Next();
            logger.LogError("Telegram login failed: {Error}. Retrying in {Delay}", remembered.Error, delay);
            await clock.DelayAsync(delay, ct);
        }
    }

    private async Task ReportFirstRunAsync(bool firstRun, CatchUpReport report, CancellationToken ct)
    {
        if (report.Processed > 0)
        {
            logger.LogInformation("Processed {Count} bot messages: {Report}", report.Processed, report);
        }

        if (firstRun && report.Processed > 0)
        {
            await notifier.NotifyAsync(
                new CatchUpNotification(report.Processed, report.Done, report.Drafted, report.Ignored, report.ParseFailed, report.Failed), ct);
        }
    }

    private async Task<TimeSpan> DelayAfterAsync(Error error, CancellationToken ct)
    {
        if (error.Code == ServiceErrors.Paused.Code)
        {
            logger.LogDebug("Processing paused by /stop");
            return PausedRecheck;
        }

        if (error.Type == ErrorType.RateLimited)
        {
            var wait = error.RetryAfter.GetValueOrDefault(TimeSpan.FromMinutes(1));
            logger.LogWarning("Pausing for {Wait}: {Error}", wait, error);
            await notifier.NotifyAsync(new PausedNotification(wait, error.Message), ct);
            return wait;
        }

        var delay = _backoff.Next();
        logger.LogError("Processing failed: {Error}. Retrying in {Delay}", error, delay);
        if (_backoff.Attempt == 3)
        {
            await notifier.NotifyAsync(new TextNotification($"Сервис не может обработать сообщения бота: {error.Message}. Повторяю с паузой."), ct);
        }

        return delay;
    }
}
