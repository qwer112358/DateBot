using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.Processing.Routes;

public abstract record MessageRoute
{
    public abstract MessageChannel Channel { get; }
}
