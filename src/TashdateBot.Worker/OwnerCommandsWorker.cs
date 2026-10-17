using TashdateBot.Application.Abstractions;
using TashdateBot.Application.UseCases;
using TashdateBot.Infrastructure.Notifier;

namespace TashdateBot.Worker;

/// <summary>Receives /start, /stop, /status and button presses from the notifier bot.</summary>
internal sealed class OwnerCommandsWorker(
    OwnerBotPoller poller,
    ICommandDispatcher dispatcher,
    ILogger<OwnerCommandsWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!poller.Enabled)
        {
            logger.LogWarning("Notifier:BotToken is empty: notifications go only to the log, owner commands are off");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var received = await poller.ReceiveAsync(stoppingToken);
            if (received.IsFailure)
            {
                logger.LogWarning("Notifier bot polling failed: {Error}", received.Error);
                await Task.Delay(RetryDelay, stoppingToken);
                continue;
            }

            foreach (var request in received.Value)
            {
                var handled = await dispatcher.SendAsync<HandleOwnerRequestCommand, Unit>(new HandleOwnerRequestCommand(request), stoppingToken);
                if (handled.IsFailure)
                {
                    logger.LogInformation("Owner request {Request} not handled: {Error}", request.GetType().Name, handled.Error);
                }
            }
        }
    }
}

/// <summary>Once a day removes data older than the configured retention.</summary>
internal sealed class RetentionWorker(ICommandDispatcher dispatcher, ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period);
        do
        {
            var purged = await dispatcher.SendAsync<PurgeExpiredDataCommand, PurgeReport>(new PurgeExpiredDataCommand(), stoppingToken);
            if (purged.IsFailure)
            {
                logger.LogWarning("Retention purge failed: {Error}", purged.Error);
            }
            else if (purged.Value.Matches + purged.Value.InboxMessages > 0)
            {
                logger.LogInformation("Retention purge removed {Matches} profiles and {Inbox} bot messages", purged.Value.Matches, purged.Value.InboxMessages);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
