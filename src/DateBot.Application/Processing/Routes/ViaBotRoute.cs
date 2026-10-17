using System.Collections.Immutable;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Application.Processing.Routes;

/// <summary>Press <see cref="ButtonLabels"/> on <see cref="Anchor"/> (if any), then send the text to the bot.</summary>
public sealed record ViaBotRoute(BotMessageId Anchor, ImmutableArray<string> ButtonLabels) : MessageRoute
{
    public override MessageChannel Channel => MessageChannel.BotChat;
}
