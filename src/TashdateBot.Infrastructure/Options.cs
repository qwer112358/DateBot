using FluentValidation;

namespace TashdateBot.Infrastructure;

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
    public string TashdateBot { get; init; } = string.Empty;

    /// <summary>Session file, relative to the data directory. Never commit it.</summary>
    public string SessionFile { get; init; } = "telegram.session";
}

public enum NotificationVerbosity
{
    All,
    ErrorsOnly,
}

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

public sealed class AnthropicOptions
{
    public const string Section = "Anthropic";

    /// <summary>Empty = no LLM, templates only.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "claude-haiku-5-5";

    public int MaxTokens { get; init; } = 2048;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
}

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>
    /// Empty = the user profile (<c>%LOCALAPPDATA%\TashdateBot</c> on Windows), outside the repository, so the
    /// session and database survive rebuilds and <c>git clean</c>. Relative paths are resolved against the app folder.
    /// </summary>
    public string DataDirectory { get; init; } = string.Empty;

    public string DatabaseFile { get; init; } = "tashdate.db";

    public string PhotosDirectory { get; init; } = "photos";

    public string ResolveDataDirectory() =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TashdateBot")
            : Path.IsPathRooted(DataDirectory) ? DataDirectory : Path.Combine(AppContext.BaseDirectory, DataDirectory);

    public string DatabasePath => Path.Combine(ResolveDataDirectory(), DatabaseFile);

    public string PhotosPath => Path.Combine(ResolveDataDirectory(), PhotosDirectory);
}

public sealed class TelegramOptionsValidator : AbstractValidator<TelegramOptions>
{
    public TelegramOptionsValidator()
    {
        RuleFor(o => o.ApiId).GreaterThan(0).WithMessage("Задайте Telegram:ApiId (my.telegram.org) в user-secrets");
        RuleFor(o => o.ApiHash).Matches("^[0-9a-fA-F]{32}$").WithMessage("Задайте Telegram:ApiHash (32 hex-символа) в user-secrets");
        RuleFor(o => o.PhoneNumber).Matches(@"^\+\d{7,15}$").WithMessage("Telegram:PhoneNumber в формате +998901234567");
        RuleFor(o => o.TashdateBot).Matches("^@?[A-Za-z][A-Za-z0-9_]{3,31}$").WithMessage("Укажите имя бота Tashdate (Telegram:TashdateBot)");
        RuleFor(o => o.SessionFile).NotEmpty();
    }
}

public sealed class NotifierOptionsValidator : AbstractValidator<NotifierOptions>
{
    public NotifierOptionsValidator()
    {
        RuleFor(o => o.BotToken).Matches(@"^(\d+:[\w-]{20,})?$").WithMessage("Notifier:BotToken не похож на токен BotFather");
        RuleFor(o => o.Verbosity).IsInEnum();
        RuleFor(o => o.QuietFrom).InclusiveBetween(TimeSpan.Zero, TimeSpan.FromHours(24));
        RuleFor(o => o.QuietTo).InclusiveBetween(TimeSpan.Zero, TimeSpan.FromHours(24));
        RuleFor(o => o.TimeZone)
            .Must(id => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _))
            .WithMessage(o => $"Неизвестный часовой пояс {o.TimeZone}");
    }
}

public sealed class AnthropicOptionsValidator : AbstractValidator<AnthropicOptions>
{
    public AnthropicOptionsValidator()
    {
        RuleFor(o => o.Model).NotEmpty();
        RuleFor(o => o.MaxTokens).InclusiveBetween(256, 16000);
        RuleFor(o => o.Timeout).GreaterThan(TimeSpan.Zero);
    }
}

public sealed class StorageOptionsValidator : AbstractValidator<StorageOptions>
{
    public StorageOptionsValidator()
    {
        RuleFor(o => o.DatabaseFile).NotEmpty();
        RuleFor(o => o.PhotosDirectory).NotEmpty();
    }
}
