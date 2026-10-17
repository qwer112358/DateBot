using DateBot.Application.Ports;

namespace DateBot.Application.Tests;

internal sealed class FakeJitter : IJitter
{
    public TimeSpan Next(TimeSpan min, TimeSpan max) => min;

    public int NextIndex(int count) => 0;
}
