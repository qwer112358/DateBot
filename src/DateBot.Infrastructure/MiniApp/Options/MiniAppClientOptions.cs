namespace DateBot.Infrastructure.MiniApp.Options;

/// <summary>Transport settings of the dating app's Mini App API.</summary>
public sealed class MiniAppClientOptions
{
    public const string Section = "MiniAppClient";

    /// <summary>Mini App address; empty = taken from the bot's menu or "Открыть знакомства" button (it may move).</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Language of the app's labels for interests, pets, education and so on.</summary>
    public string Language { get; init; } = "ru";

    /// <summary>Sent as User-Agent when set; empty = no header.</summary>
    public string UserAgent { get; init; } = string.Empty;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long one Telegram sign-in (init data) is used before a fresh one is requested.</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromMinutes(30);
}
