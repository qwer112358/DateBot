using System.Collections.Immutable;
using TashdateBot.Domain.Matches;
using TashdateBot.Domain.Shared;

namespace TashdateBot.Application.Processing;

public abstract record MessageRoute;

/// <summary>Press <see cref="ButtonLabels"/> on <see cref="Anchor"/> (if any), then send the text to the bot.</summary>
public sealed record ViaBotRoute(BotMessageId Anchor, ImmutableArray<string> ButtonLabels) : MessageRoute;

public sealed record DirectRoute(ContactUsername Username) : MessageRoute;

public static class MessageRouter
{
    /// <param name="mutual">True when the person has liked us back (mutual notification or scenario B).</param>
    public static MessageRoute Choose(Profile profile, bool mutual, BotMessageId anchor, ProcessingOptions options) =>
        mutual && options.MutualRoute == MessageRouteKind.DirectToUser && profile.Username.HasValue
            ? new DirectRoute(profile.Username.GetValueOrThrow())
            : new ViaBotRoute(anchor, [.. options.MessageButtons]);
}
