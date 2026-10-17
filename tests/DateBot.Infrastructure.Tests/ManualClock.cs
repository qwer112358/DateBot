using DateBot.Application.Ports;

namespace DateBot.Infrastructure.Tests;

internal sealed class ManualClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        UtcNow += delay;
        return Task.CompletedTask;
    }
}
