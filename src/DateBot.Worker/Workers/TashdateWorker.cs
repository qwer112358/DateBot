using DateBot.Application.Abstractions.Errors;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Enums;
using DateBot.Application.MiniApp.Options;
using DateBot.Application.MiniApp.Services;
using DateBot.Application.Notifications;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Models;
using DateBot.Domain.Common.Errors;
using Microsoft.Extensions.Options;

namespace DateBot.Worker.Workers;

/// <summary>
/// Main loop: log in, catch up on everything missed in the bot chat, handle the likes waiting in the app, then wait
/// for the bot to write (or for the next poll) and repeat. Bot chat and app run one after the other, never at the
/// same time, so a match card never races with the like back that caused it. The app has its own schedule: its
/// daily like limit pauses only the likes, never the bot chat. /likes and your answers on like cards only wake the
/// loop (<see cref="AppCheckSignal"/>), so every app action still happens here.
/// </summary>
internal sealed class TashdateWorker(
    ITashdateClient client,
    ICommandDispatcher dispatcher,
    IOwnerNotifier notifier,
    AppCheckSignal appCheck,
    IClock clock,
    IOptions<ProcessingOptions> options,
    ILogger<TashdateWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PausedRecheck = TimeSpan.FromSeconds(30);
    private readonly Backoff _backoff = new(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5));
    private readonly Backoff _appBackoff = new(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
    private DateTimeOffset _nextAppSync = DateTimeOffset.MinValue;
    private bool _appHeld;
    private bool _appPauseNotified;

    private MiniAppOptions MiniApp => options.Value.MiniApp;

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
                var asked = appCheck.Take();
                if (asked != AppCheckRequest.None || (caughtUp.Value.LikeHints > 0 && !_appHeld))
                {
                    _nextAppSync = clock.UtcNow;
                }

                await SyncAppIfDueAsync(asked == AppCheckRequest.Report, stoppingToken);
                await WaitAsync(stoppingToken);
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

    /// <summary>The bot-chat poll, or sooner when the app's turn comes first.</summary>
    private TimeSpan NextWait()
    {
        var poll = options.Value.PollInterval;
        if (!MiniApp.Enabled)
        {
            return poll;
        }

        var untilApp = _nextAppSync - clock.UtcNow;
        return untilApp < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : untilApp < poll ? untilApp : poll;
    }

    /// <summary>Waits for the bot to write, the next poll, or a check you asked for, whichever comes first.</summary>
    private async Task WaitAsync(CancellationToken stoppingToken)
    {
        using var wake = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var activity = client.WaitForActivityAsync(NextWait(), wake.Token);
        var asked = appCheck.WaitAsync(wake.Token);
        await Task.WhenAny(activity, asked);
        await wake.CancelAsync();
        try
        {
            await Task.WhenAll(activity, asked);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // The wait that lost was cut short on purpose.
        }
    }

    /// <param name="report">/likes asked for it: you get a summary, or the reason the check failed.</param>
    private async Task SyncAppIfDueAsync(bool report, CancellationToken ct)
    {
        if (!MiniApp.Enabled || clock.UtcNow < _nextAppSync)
        {
            return;
        }

        var synced = await dispatcher.SendAsync<SyncAppLikesCommand, AppSyncReport>(new SyncAppLikesCommand(ResendWaiting: report), ct);
        if (report)
        {
            await notifier.NotifyAsync(synced.IsSuccess
                ? new AppLikesCheckedNotification(synced.Value)
                : new TextNotification($"Проверить лайки в приложении не получилось: {synced.Error.Message}"), ct);
        }

        if (synced.IsSuccess)
        {
            _appBackoff.Reset();
            _appHeld = false;
            _appPauseNotified = false;
            _nextAppSync = clock.UtcNow + MiniApp.PollInterval;
            if (synced.Value.Handled > 0 || synced.Value.Failed > 0)
            {
                logger.LogInformation("App likes handled: {Report}", synced.Value);
            }

            return;
        }

        _nextAppSync = clock.UtcNow + await AppDelayAfterAsync(synced.Error, ct);
    }

    /// <summary>
    /// A pause or limit holds only the app's likes until it passes (the owner hears about it once); other errors back
    /// off and are reported on the third attempt in a row.
    /// </summary>
    private async Task<TimeSpan> AppDelayAfterAsync(Error error, CancellationToken ct)
    {
        if (error.Code == ServiceErrors.Paused.Code)
        {
            return PausedRecheck;
        }

        _appHeld = true;
        if (error.Type == ErrorType.RateLimited)
        {
            var wait = error.RetryAfter.GetValueOrDefault(TimeSpan.FromHours(1));
            logger.LogWarning("App likes paused for {Wait}: {Error}", wait, error);
            if (!_appPauseNotified && error.Code != "service.hourly_limit")
            {
                _appPauseNotified = true;
                await notifier.NotifyAsync(
                    new PausedNotification(wait, $"{error.Message}. Ответы на лайки продолжатся позже; мэтчи и сообщения работают."), ct);
            }

            return wait;
        }

        var delay = _appBackoff.Next();
        logger.LogError("App likes failed: {Error}. Retrying in {Delay}", error, delay);
        if (_appBackoff.Attempt == 3)
        {
            await notifier.NotifyAsync(
                new TextNotification($"Не получается ответить на лайки в приложении: {error.Message}. Повторяю с паузой, чат бота работает."), ct);
        }

        return delay;
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
