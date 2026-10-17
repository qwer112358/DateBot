using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.UseCases.Models;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Infrastructure.Notifier.Models;

namespace DateBot.Infrastructure.Notifier.Services;

/// <summary>Pure: notification → HTML text and inline buttons for the notifier bot.</summary>
public static class NotificationFormatter
{
    public const string SendPrefix = "send:";
    public const string SendToTelegramPrefix = "send:tg:";
    public const string SendToAppPrefix = "send:app:";
    public const string SendViaBotPrefix = "send:bot:";
    public const string LookUpPrefix = "find:";
    public const string SkipPrefix = "skip:";
    public const string ForgetPrefix = "forget:";
    public const string LikeBackPrefix = "like:";
    public const string NoLikeBackPrefix = "nolike:";

    public static FormattedNotification Format(OwnerNotification notification) => notification switch
    {
        LikeHandledNotification n => About(
            n.Profile,
            $"❤️ <b>Лайк обработан</b>\n{Header(n.Profile)}\nСделано: {(n.LikedBack ? "лайк в ответ + сообщение" : "сообщение")} {Where(n.Channel)}\n\n«{E(n.Message.Value)}»",
            Forget(n.MatchId)),
        LikedBackNotification n => About(
            n.Profile,
            $"❤️ <b>Лайк в ответ</b>\n{Header(n.Profile)}\nСообщение уйдёт после взаимной симпатии.",
            Forget(n.MatchId)),
        MutualHandledNotification n => About(
            n.Profile,
            $"💞 <b>Взаимная симпатия</b>\n{Header(n.Profile)}\nОтправлено {Where(n.Channel)}: «{E(n.Message.Value)}»",
            Forget(n.MatchId)),
        DraftReadyNotification { Channels.IsDefaultOrEmpty: true } n => new FormattedNotification(
            $"✍️ <b>Черновик</b> ({DraftReason(n)})\n{Header(n.Profile)}{Contacts(n.Profile)}{Details(n.Profile)}\n\n<b>Черновик:</b>\n«{E(n.Draft.Value)}»\n\n"
                + "Отправить некуда: у человека нет @username, а в приложении анкета не найдена. Скопируйте текст и напишите в приложении сами.",
            [new InlineAction("🔄 Найти в приложении", LookUpPrefix + n.MatchId), new InlineAction("⏭ Пропустить", SkipPrefix + n.MatchId)],
            n.Profile.Photo),
        DraftReadyNotification n => new FormattedNotification(
            $"✍️ <b>Черновик</b> ({DraftReason(n)})\n{Header(n.Profile)}{Contacts(n.Profile)}{Details(n.Profile)}\n\n<b>Черновик:</b>\n«{E(n.Draft.Value)}»\n\nКуда отправить?",
            [.. n.Channels.Select(channel => SendButton(channel, n.MatchId)), new InlineAction("⏭ Пропустить", SkipPrefix + n.MatchId)],
            n.Profile.Photo),
        LikeBackProposedNotification n => About(
            n.Profile,
            $"💛 <b>Вас лайкнули</b>\n{Header(n.Profile)}{Details(n.Profile)}\n\nОтветить взаимным лайком?",
            new InlineAction("❤️ Ответить", LikeBackPrefix + n.MatchId),
            new InlineAction("⏭ Пропустить", NoLikeBackPrefix + n.MatchId)),
        AppLikesCheckedNotification n => Text(Likes(n.Report)),
        ParseFailedNotification n => Text(
            $"⚠️ <b>Не удалось разобрать сообщение бота</b> #{n.BotMessageId}\nПричина: {E(n.Reason)}\n\n<pre>{E(Truncate(n.RawText, 2500))}</pre>"),
        ActionFailedNotification n => About(
            n.Profile,
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

    /// <summary>Every notification about a person comes with their photo.</summary>
    private static FormattedNotification About(Profile profile, string html, params InlineAction[] buttons) =>
        new(html, [.. buttons], profile.Photo);

    private static InlineAction Forget(MatchId id) => new("🗑 Забыть", ForgetPrefix + id);

    private static InlineAction SendButton(MessageChannel channel, MatchId id) => channel switch
    {
        MessageChannel.Telegram => new("✈️ В Telegram", SendToTelegramPrefix + id),
        MessageChannel.App => new("💬 В приложении", SendToAppPrefix + id),
        _ => new("🤖 Через бота", SendViaBotPrefix + id),
    };

    private static string Where(MessageChannel channel) => channel switch
    {
        MessageChannel.Telegram => "в личку Telegram",
        MessageChannel.App => "в чат приложения",
        _ => "через бота",
    };

    private static string DraftReason(DraftReadyNotification n) => (n.Mutual, n.Kind) switch
    {
        (true, MatchKind.LikedMe) => "мэтч: вы ответили на лайк",
        (true, _) => "мэтч",
        _ => "вас лайкнули",
    };

    private static string Contacts(Profile profile) =>
        profile.Username.HasValue ? $"\nTelegram: {E(profile.Username.GetValueOrThrow().ToString())}" : string.Empty;

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

        foreach (var fact in profile.Facts.Items)
        {
            text.Append('\n').Append(E(fact));
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

    private static string Likes(AppSyncReport report)
    {
        var text = new StringBuilder($"💛 <b>Лайки в приложении</b>: открытых {report.Open}");
        if (report.Hidden > 0)
        {
            text.Append($", скрытых до премиума {report.Hidden}");
        }

        (int Count, string Line)[] lines =
        [
            (report.Proposed, "🆕 новых карточек"),
            (report.Waiting, "⏳ ждут вашего ответа (карточки выше)"),
            (report.Answered, "❤️ лайк в ответ поставлен"),
            (report.Ignored, "✔️ уже разобраны"),
            (report.Failed, "❌ ошибок"),
            (report.Deferred, "⏭ не дошла очередь, будут при следующей проверке"),
        ];
        foreach (var (count, line) in lines.Where(l => l.Count > 0))
        {
            text.Append($"\n{line}: {count}");
        }

        return report.Open == 0 ? text.Append("\nОтвечать пока не на что.").ToString() : text.ToString();
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
