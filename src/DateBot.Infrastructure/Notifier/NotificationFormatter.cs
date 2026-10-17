using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.UseCases;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;

namespace DateBot.Infrastructure.Notifier;

/// <summary>Pure: notification → HTML text and inline buttons for the notifier bot.</summary>
public static class NotificationFormatter
{
    public const string SendPrefix = "send:";
    public const string SkipPrefix = "skip:";
    public const string ForgetPrefix = "forget:";

    public static FormattedNotification Format(OwnerNotification notification) => notification switch
    {
        LikeHandledNotification n => Text(
            $"❤️ <b>Лайк обработан</b>\n{Header(n.Profile)}\nСделано: {(n.LikedBack ? "лайк в ответ + сообщение" : "сообщение")}\n\n«{E(n.Message.Value)}»",
            Forget(n.MatchId)),
        LikedBackNotification n => Text(
            $"❤️ <b>Лайк в ответ</b>\n{Header(n.Profile)}\nСообщение уйдёт после взаимной симпатии.",
            Forget(n.MatchId)),
        MutualHandledNotification n => Text(
            $"💞 <b>Взаимная симпатия</b>\n{Header(n.Profile)}\nОтправлено: «{E(n.Message.Value)}»",
            Forget(n.MatchId)),
        DraftReadyNotification n => new FormattedNotification(
            $"✍️ <b>Черновик</b> ({(n.Kind == MatchKind.Mutual ? "взаимная симпатия" : "вас лайкнули")})\n{Header(n.Profile)}{Details(n.Profile)}\n\n<b>Черновик:</b>\n«{E(n.Draft.Value)}»",
            [new InlineAction("✅ Отправить", SendPrefix + n.MatchId), new InlineAction("⏭ Пропустить", SkipPrefix + n.MatchId)],
            n.Profile.Photo),
        ParseFailedNotification n => Text(
            $"⚠️ <b>Не удалось разобрать сообщение бота</b> #{n.BotMessageId}\nПричина: {E(n.Reason)}\n\n<pre>{E(Truncate(n.RawText, 2500))}</pre>"),
        ActionFailedNotification n => Text(
            $"❌ <b>Ошибка</b>\n{Header(n.Profile)}\nПричина: {E(n.Reason.Message)}",
            Forget(n.MatchId)),
        PausedNotification n => Text(
            $"⏸ <b>Пауза {FormatDuration(n.Duration)}</b>\n{E(n.Reason)}"),
        CatchUpNotification n => Text(
            $"🔄 <b>После простоя дочитано {n.Processed}</b>\n✅ обработано: {n.Done}\n✍️ черновиков: {n.Drafted}\n⏭ пропущено: {n.Ignored}\n⚠️ не разобрано: {n.ParseFailed}\n❌ ошибок: {n.Failed}"),
        StatusNotification n => Text(Status(n.Report)),
        TextNotification n => Text(E(n.Text)),
        _ => Text(E(notification.ToString())),
    };

    private static FormattedNotification Text(string html, params InlineAction[] buttons) =>
        new(html, [.. buttons], Maybe<PhotoFileId>.None);

    private static InlineAction Forget(MatchId id) => new("🗑 Забыть", ForgetPrefix + id);

    private static string Header(Profile profile)
    {
        var parts = new List<string> { profile.Name.Value };
        if (profile.Age.HasValue)
        {
            parts.Add(profile.Age.GetValueOrThrow().ToString());
        }

        if (profile.City.HasValue)
        {
            parts.Add(profile.City.GetValueOrThrow().Value);
        }

        return $"<b>{E(string.Join(", ", parts))}</b>";
    }

    private static string Details(Profile profile)
    {
        var text = new StringBuilder();
        if (profile.Bio.HasValue)
        {
            text.Append("\n").Append(E(Truncate(profile.Bio.GetValueOrThrow().Value, 600)));
        }

        if (!profile.Interests.IsEmpty)
        {
            text.Append("\nИнтересы: ").Append(E(profile.Interests.ToString()));
        }

        return text.ToString();
    }

    private static string Status(StatusReport report)
    {
        var text = new StringBuilder()
            .AppendLine("📊 <b>Статус</b>")
            .AppendLine($"Состояние: {(report.Paused ? "остановлен (/start — продолжить)" : "работает")}")
            .AppendLine($"Режим: {report.Mode}")
            .AppendLine($"Курсор: {report.Cursor}" + (report.LastProcessedAt.HasValue
                ? $" ({report.LastProcessedAt.GetValueOrThrow().ToString("dd.MM HH:mm", CultureInfo.InvariantCulture)} UTC)"
                : string.Empty))
            .AppendLine($"Действий за час: {report.ActionsLastHour} из {report.MaxActionsPerHour}")
            .AppendLine($"Сообщения бота: {Counts(report.Inbox, InboxNames)}")
            .Append($"Анкеты: {Counts(report.Matches, MatchNames)}");
        return text.ToString();
    }

    private static readonly Dictionary<InboxStatus, string> InboxNames = new()
    {
        [InboxStatus.Done] = "обработано",
        [InboxStatus.Ignored] = "пропущено",
        [InboxStatus.ParseFailed] = "не разобрано",
        [InboxStatus.Failed] = "ошибки",
        [InboxStatus.Received] = "в работе",
    };

    private static readonly Dictionary<MatchStatus, string> MatchNames = new()
    {
        [MatchStatus.Done] = "готово",
        [MatchStatus.LikedBack] = "лайк, ждём",
        [MatchStatus.Received] = "новые",
        [MatchStatus.MessageSent] = "отправлено",
        [MatchStatus.Failed] = "ошибки",
        [MatchStatus.Skipped] = "пропущено",
    };

    private static string Counts<TKey>(IReadOnlyDictionary<TKey, int> counts, Dictionary<TKey, string> names)
        where TKey : notnull =>
        counts.Count == 0
            ? "нет"
            : string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{names.GetValueOrDefault(c.Key, c.Key.ToString() ?? "?")} {c.Value}"));

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalMinutes >= 1 ? $"на {Math.Ceiling(duration.TotalMinutes)} мин" : $"на {Math.Ceiling(duration.TotalSeconds)} с";

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";

    private static string E(string text) => WebUtility.HtmlEncode(text);
}
