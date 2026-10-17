namespace DateBot.Infrastructure.Telegram.Options;

/// <summary>Your Telegram account (userbot). Secrets go to user-secrets or environment variables.</summary>
public sealed class TelegramOptions
{
    public const string Section = "Telegram";

    /// <summary>From my.telegram.org.</summary>
    public int ApiId { get; init; }

    public string ApiHash { get; init; } = string.Empty;

    /// <summary>International format, e.g. +998901234567.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Two-step verification password; empty = asked in the console when needed.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Username of the Tashdate bot, without the at-sign.</summary>
    public string TashdateBotUsername { get; init; } = string.Empty;

    /// <summary>Session file, relative to the data directory. Never commit it.</summary>
    public string SessionFile { get; init; } = "telegram.session";
}
