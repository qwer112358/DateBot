using DateBot.Application.MiniApp.Enums;

namespace DateBot.Application.MiniApp.Options;

/// <summary>Work through the dating app's Mini App: answering incoming likes and its in-app chat.</summary>
public sealed class MiniAppOptions
{
    /// <summary>Off = only the bot chat is read; likes are answered by hand and the in-app chat is not offered.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Confirm: a new like comes to you as a card and is liked back only after your yes. Auto: at once.</summary>
    public LikeBackMode LikeBack { get; init; } = LikeBackMode.Confirm;

    /// <summary>How often the list of incoming likes is checked; a like hint in the bot chat or /likes checks it at once.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>At most this many new likes are answered or sent to you as cards per check; the rest wait for the next one.</summary>
    public int MaxLikesPerPass { get; init; } = 10;
}
