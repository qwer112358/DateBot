namespace DateBot.Worker;

/// <summary>Exponential back-off for reconnecting.</summary>
internal sealed class Backoff(TimeSpan initial, TimeSpan max)
{
    public int Attempt { get; private set; }

    public TimeSpan Next()
    {
        Attempt++;
        var delay = TimeSpan.FromTicks(initial.Ticks * (1L << Math.Min(Attempt - 1, 10)));
        return delay > max ? max : delay;
    }

    public void Reset() => Attempt = 0;
}
