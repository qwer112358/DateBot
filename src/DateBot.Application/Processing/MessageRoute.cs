using DateBot.Domain.Matches;

namespace DateBot.Application.Processing;

public abstract record MessageRoute
{
    public abstract MessageChannel Channel { get; }
}
