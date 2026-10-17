namespace DateBot.Application.Ports.Services;

public interface IJitter
{
    TimeSpan Next(TimeSpan min, TimeSpan max);

    int NextIndex(int count);
}
