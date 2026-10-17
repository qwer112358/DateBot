using DateBot.Application.Ports;

namespace DateBot.Application.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        Delays.Add(delay);
        UtcNow += delay;
        return Task.CompletedTask;
    }
}
