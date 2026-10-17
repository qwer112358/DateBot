using DateBot.Infrastructure.Notifier.Enums;

namespace DateBot.Infrastructure.Notifier.Options;

/// <summary>The separate notifier bot (Bot API) that writes to you.</summary>
public sealed class NotifierOptions
{
    public const string Section = "Notifier";

    /// <summary>Token from BotFather; empty disables notifications (they are only logged).</summary>
    public string BotToken { get; init; } = string.Empty;

    public NotificationVerbosity Verbosity { get; init; } = NotificationVerbosity.All;

    public bool QuietHoursEnabled { get; init; }

    public TimeSpan QuietFrom { get; init; } = TimeSpan.FromHours(23);

    public TimeSpan QuietTo { get; init; } = TimeSpan.FromHours(8);

    /// <summary>IANA or Windows time zone id for quiet hours.</summary>
    public string TimeZone { get; init; } = "Asia/Tashkent";
}
