using System.Collections.Immutable;
using DateBot.Domain.Shared;

namespace DateBot.Application.Processing;

/// <summary>Press <see cref="ButtonLabels"/> on <see cref="Anchor"/> (if any), then send the text to the bot.</summary>
public sealed record ViaBotRoute(BotMessageId Anchor, ImmutableArray<string> ButtonLabels) : MessageRoute;
