using DateBot.Application.Abstractions;
using DateBot.Application.UseCases;
using DateBot.Infrastructure.Notifier;

namespace DateBot.Worker;

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
