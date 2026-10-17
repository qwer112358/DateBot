using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Application.Processing;

public static class MessageRouter
{
    /// <param name="mutual">True when the person has liked us back (mutual notification or scenario B).</param>
    public static MessageRoute Choose(Profile profile, bool mutual, BotMessageId anchor, ProcessingOptions options) =>
        mutual && options.MutualRoute == MessageRouteKind.DirectToUser && profile.Username.HasValue
            ? new DirectRoute(profile.Username.GetValueOrThrow())
            : new ViaBotRoute(anchor, [.. options.MessageButtons]);
}
