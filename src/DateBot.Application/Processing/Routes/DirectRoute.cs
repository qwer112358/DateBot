using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Processing.Routes;

public sealed record DirectRoute(ContactUsername Username) : MessageRoute
{
    public override MessageChannel Channel => MessageChannel.Telegram;
}
