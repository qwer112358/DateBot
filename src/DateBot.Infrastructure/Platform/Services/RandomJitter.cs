using DateBot.Application.Ports.Services;

namespace DateBot.Infrastructure.Platform.Services;

internal sealed class RandomJitter : IJitter
{
    public TimeSpan Next(TimeSpan min, TimeSpan max) =>
        max <= min ? min : min + TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * (max - min).TotalMilliseconds);

    public int NextIndex(int count) => count <= 1 ? 0 : Random.Shared.Next(count);
}
