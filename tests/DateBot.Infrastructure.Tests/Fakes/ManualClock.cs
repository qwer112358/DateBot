using DateBot.Application.Ports.Services;

namespace DateBot.Infrastructure.Tests.Fakes;

internal sealed class ManualClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        UtcNow += delay;
        return Task.CompletedTask;
    }
}
