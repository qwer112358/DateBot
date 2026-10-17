using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Models;

namespace DateBot.Worker.Workers;

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
