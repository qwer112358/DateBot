namespace DateBot.Domain.Matches;

/// <summary>Where the message to the person goes.</summary>
public enum MessageChannel
{
    /// <summary>Into the chat with the dating bot, which forwards it to the profile it shows.</summary>
    BotChat = 1,

    /// <summary>Direct message in Telegram by the username revealed on a match.</summary>
    Telegram = 2,

    /// <summary>The chat inside the dating app (Mini App).</summary>
    App = 3,
}
