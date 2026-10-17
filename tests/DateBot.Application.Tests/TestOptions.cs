using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Parsing;
using DateBot.Application.Processing;
using DateBot.Application.Replies;
using DateBot.Application.Telegram;

namespace DateBot.Application.Tests;

/// <summary>Same rules as the shipped appsettings.json.</summary>
internal static class TestOptions
{
    public static ParsingOptions Parsing() => new()
    {
        LikeMarkers = ["понравил", "лайкнул", "yoqtir", "liked you"],
        MutualMarkers = ["взаимн", "o'zaro", "o‘zaro", "mutual", "it's a match"],
        IgnoreMarkers = ["Лайк отправлен"],
        ProfilePattern = @"^(?<name>[^,\n]{1,64}),\s*(?<age>\d{2})\s*(?:,\s*(?<city>[^\n,–—-]{1,64}?))?\s*(?:[–—-]\s*(?<bio>[\s\S]+))?$",
        InterestsPattern = @"^(?:Интересы|Увлечения|Qiziqishlar|Interests)\s*:\s*(?<interests>.+)$",
        UsernamePattern = @"@(?<username>[A-Za-z][A-Za-z0-9_]{4,31})",
        PersonIdPattern = string.Empty,
        UnknownMessages = UnknownMessagePolicy.ParseFailed,
    };

    public static ParsingRules Rules() => ParsingRules.From(Parsing());

    public static ProcessingOptions Processing(
        ProcessingMode mode = ProcessingMode.Auto,
        LikedMeMessagePolicy likedMe = LikedMeMessagePolicy.AfterLikeBack,
        string[]? messageButtons = null,
        TimeSpan? firstRunBackfill = null) => new()
    {
        FirstRunBackfill = firstRunBackfill ?? TimeSpan.FromDays(1),
        Mode = mode,
        LikedMeMessage = likedMe,
        MutualRoute = MessageRouteKind.DirectToUser,
        LikeBackButtons = ["❤"],
        MessageButtons = messageButtons ?? ["💌"],
        MinPause = TimeSpan.FromSeconds(5),
        MaxPause = TimeSpan.FromSeconds(30),
        ButtonToTextPause = TimeSpan.FromSeconds(3),
        MaxActionsPerHour = 20,
        HistoryBatchSize = 2,
    };

    public static ReplyOptions Replies(InvalidReplyPolicy policy = InvalidReplyPolicy.Template) => new()
    {
        Owner = new OwnerProfileOptions { Name = "Рустам", About = "разработчик", Tone = "дружелюбный", Avoid = "работа" },
        DefaultLanguage = ReplyLanguage.Russian,
        MinLength = 20,
        MaxLength = 400,
        MaxEmoji = 1,
        UsePhoto = true,
        InvalidReplyPolicy = policy,
        Templates = new()
        {
            ["Russian"] = ["Привет, {name}! Приятно, что мы совпали. Как проходит твой день?"],
            ["Uzbek"] = ["Salom, {name}! Tanishganimizdan xursandman."],
            ["English"] = ["Hi {name}! Glad we matched."],
        },
    };
}

/// <summary>Anonymised examples of bot notifications (format to be confirmed with real Tashdate messages).</summary>
internal static class Samples
{
    public static readonly DateTimeOffset SentAt = new(2026, 10, 17, 11, 0, 0, TimeSpan.Zero);

    public const string AnnaProfile = "Анна, 24, Ташкент – Люблю горы, кофе и долгие прогулки";

    public static RawBotMessage Like(long id, string profile = AnnaProfile, bool photo = true) => new(
        id,
        SentAt,
        $"Ты понравился 1 человеку:\n\n{profile}",
        [new BotButton("❤️", 0, 0), new BotButton("👎", 0, 1), new BotButton("💌", 0, 2)],
        photo ? Maybe.From("photo:111") : Maybe<string>.None);

    public static RawBotMessage Mutual(long id, string profile = AnnaProfile, string username = "anna_tash") => new(
        id,
        SentAt,
        $"Есть взаимная симпатия! Начинай общаться 👉 @{username}\n\n{profile}",
        ImmutableArray<BotButton>.Empty,
        Maybe<string>.None);

    public static RawBotMessage Text(long id, string text) =>
        new(id, SentAt, text, ImmutableArray<BotButton>.Empty, Maybe<string>.None);
}
