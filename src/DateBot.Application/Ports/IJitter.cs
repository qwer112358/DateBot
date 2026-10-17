namespace DateBot.Application.Ports;

public interface IJitter
{
    TimeSpan Next(TimeSpan min, TimeSpan max);

    int NextIndex(int count);
}
