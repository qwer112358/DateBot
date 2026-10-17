using FluentValidation;

namespace DateBot.Application.Processing;

public enum ProcessingMode
{
    /// <summary>Messages are sent automatically.</summary>
    Auto,

    /// <summary>Drafts go to the notifier bot; sending waits for your "Send".</summary>
    Confirm,
}

/// <summary>When to send the message in scenario A (open question: does Tashdate allow it before a mutual like?).</summary>
public enum LikedMeMessagePolicy
{
    /// <summary>Right after the like back.</summary>
    AfterLikeBack,

    /// <summary>Only like back; the message goes when the mutual-like notification arrives.</summary>
    OnMutual,
}

public enum MessageRouteKind
{
    /// <summary>Press the message button (if configured) and send the text into the bot chat.</summary>
    ViaBot,

    /// <summary>Write to the person directly by the username the bot revealed; falls back to ViaBot without one.</summary>
    DirectToUser,
}

public sealed class ProcessingOptions
{
    public const string Section = "Processing";

    public ProcessingMode Mode { get; init; } = ProcessingMode.Auto;

    public LikedMeMessagePolicy LikedMeMessage { get; init; } = LikedMeMessagePolicy.AfterLikeBack;

    public MessageRouteKind MutualRoute { get; init; } = MessageRouteKind.DirectToUser;

    /// <summary>Labels of the like-back button, matched by "contains".</summary>
    public string[] LikeBackButtons { get; init; } = [];

    /// <summary>Labels of the "write a message" button for the ViaBot route; empty = just send the text.</summary>
    public string[] MessageButtons { get; init; } = [];

    public TimeSpan MinPause { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxPause { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Pause between pressing the message button and sending the text.</summary>
    public TimeSpan ButtonToTextPause { get; init; } = TimeSpan.FromSeconds(3);

    public int MaxActionsPerHour { get; init; } = 20;

    public int HistoryBatchSize { get; init; } = 50;

    /// <summary>Safety-net poll of the bot chat in case a live update was missed.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// First run only (no cursor yet): how far back to read the bot chat. 0 = only new notifications,
    /// so old likes from the history are not answered months later.
    /// </summary>
    public TimeSpan FirstRunBackfill { get; init; } = TimeSpan.Zero;

    /// <summary>Data retention in days; 0 keeps everything.</summary>
    public int RetentionDays { get; init; }

    public FilterOptions Filters { get; init; } = new();
}

/// <summary>Placeholder for profile filters. Disabled by default: everyone gets an answer.</summary>
public sealed class FilterOptions
{
    public bool Enabled { get; init; }

    public int MinAge { get; init; } = 18;

    public int MaxAge { get; init; } = 99;

    /// <summary>Empty = any city.</summary>
    public string[] Cities { get; init; } = [];

    /// <summary>Profiles whose text contains any of these words are skipped.</summary>
    public string[] StopWords { get; init; } = [];
}

public sealed class ProcessingOptionsValidator : AbstractValidator<ProcessingOptions>
{
    public ProcessingOptionsValidator()
    {
        RuleFor(o => o.Mode).IsInEnum();
        RuleFor(o => o.LikedMeMessage).IsInEnum();
        RuleFor(o => o.MutualRoute).IsInEnum();
        RuleFor(o => o.LikeBackButtons).NotEmpty().WithMessage("Укажите подписи кнопки лайка в ответ (Processing:LikeBackButtons)");
        RuleFor(o => o.MinPause).GreaterThanOrEqualTo(TimeSpan.Zero);
        RuleFor(o => o.MaxPause).GreaterThanOrEqualTo(o => o.MinPause).WithMessage("MaxPause должна быть не меньше MinPause");
        RuleFor(o => o.ButtonToTextPause).GreaterThanOrEqualTo(TimeSpan.Zero);
        RuleFor(o => o.MaxActionsPerHour).GreaterThan(0);
        RuleFor(o => o.HistoryBatchSize).InclusiveBetween(1, 100);
        RuleFor(o => o.PollInterval).GreaterThan(TimeSpan.Zero);
        RuleFor(o => o.RetentionDays).GreaterThanOrEqualTo(0);
        RuleFor(o => o.FirstRunBackfill).InclusiveBetween(TimeSpan.Zero, TimeSpan.FromDays(30));
        RuleFor(o => o.Filters.MinAge).InclusiveBetween(18, 99);
        RuleFor(o => o.Filters.MaxAge).GreaterThanOrEqualTo(o => o.Filters.MinAge);
    }
}
