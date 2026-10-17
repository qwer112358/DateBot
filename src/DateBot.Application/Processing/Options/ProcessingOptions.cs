using DateBot.Application.MiniApp.Options;
using DateBot.Application.Processing.Enums;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.Processing.Options;

public sealed class ProcessingOptions
{
    public const string Section = "Processing";

    public ProcessingMode Mode { get; init; } = ProcessingMode.Auto;

    public LikedMeMessagePolicy LikedMeMessage { get; init; } = LikedMeMessagePolicy.AfterLikeBack;

    public MessageRouteKind MutualRoute { get; init; } = MessageRouteKind.DirectToUser;

    /// <summary>
    /// Channels a message may go through; empty = all. Tashdate's bot does not forward text, so its config lists
    /// only Telegram and App.
    /// </summary>
    public MessageChannel[] Channels { get; init; } = [];

    public MiniAppOptions MiniApp { get; init; } = new();

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

    public bool IsEnabled(MessageChannel channel) => Channels.Length == 0 || Channels.Contains(channel);
}
