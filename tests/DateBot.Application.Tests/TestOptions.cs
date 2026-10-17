using DateBot.Application.Parsing;
using DateBot.Application.Processing;
using DateBot.Application.Replies;

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
