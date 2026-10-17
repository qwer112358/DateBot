using DateBot.Application.Ports.Services;

namespace DateBot.Application.Tests.Fakes;

internal sealed class FakeJitter : IJitter
{
    public TimeSpan Next(TimeSpan min, TimeSpan max) => min;

    public int NextIndex(int count) => 0;
}
