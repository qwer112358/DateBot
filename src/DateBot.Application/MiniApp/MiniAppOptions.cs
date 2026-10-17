namespace DateBot.Application.MiniApp;

/// <summary>Work through the dating app's Mini App: answering incoming likes and its in-app chat.</summary>
public sealed class MiniAppOptions
{
    /// <summary>Off = only the bot chat is read; likes are answered by hand and the in-app chat is not offered.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How often the list of incoming likes is checked; a like hint in the bot chat checks it at once.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>At most this many likes are answered per check; the rest wait for the next one.</summary>
    public int MaxLikesPerPass { get; init; } = 10;
}
