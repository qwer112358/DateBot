using DateBot.Application.Ports;

namespace DateBot.Infrastructure.Platform;

internal sealed class SystemClock(TimeProvider time) : IClock
{
    public DateTimeOffset UtcNow => time.GetUtcNow();

    public Task DelayAsync(TimeSpan delay, CancellationToken ct) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, time, ct);
}
