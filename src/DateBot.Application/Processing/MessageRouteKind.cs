namespace DateBot.Application.Processing;

public enum MessageRouteKind
{
    /// <summary>Press the message button (if configured) and send the text into the bot chat.</summary>
    ViaBot,

    /// <summary>Write to the person directly by the username the bot revealed; falls back to ViaBot without one.</summary>
    DirectToUser,
}
