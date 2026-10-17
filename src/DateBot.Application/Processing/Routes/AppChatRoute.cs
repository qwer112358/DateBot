using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Processing.Routes;

/// <param name="ClientKey">Same for every attempt of this message, so the app can drop a repeat.</param>
public sealed record AppChatRoute(AppProfileId To, string ClientKey) : MessageRoute
{
    public override MessageChannel Channel => MessageChannel.App;
}
