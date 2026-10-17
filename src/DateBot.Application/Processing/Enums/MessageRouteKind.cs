namespace DateBot.Application.Processing.Enums;

/// <summary>Preferred channel for a message after a match, used in Auto mode (in Confirm mode you pick it).</summary>
public enum MessageRouteKind
{
    /// <summary>Press the message button (if configured) and send the text into the bot chat.</summary>
    ViaBot,

    /// <summary>Write to the person directly by the username the bot revealed; otherwise the next available channel.</summary>
    DirectToUser,

    /// <summary>Write in the chat inside the dating app; otherwise the next available channel.</summary>
    AppChat,
}
