using DateBot.Application.MiniApp.Enums;
using DateBot.Application.MiniApp.Options;
using DateBot.Application.Parsing.Enums;
using DateBot.Application.Parsing.Models;
using DateBot.Application.Parsing.Options;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Processing.Options;
using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Options;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.Tests.Support;

internal static class TestOptions
{
    private const string FullProfilePattern =
        @"^(?<name>[^,\n]{1,64}),\s*(?<age>\d{2})\s*(?:,\s*(?<city>[^\n,–—-]{1,64}?))?\s*(?:[–—-]\s*(?<bio>[\s\S]+))?$";

    private const string UsernamePattern = @"@(?<username>[A-Za-z][A-Za-z0-9_]{4,31})";

    /// <summary>Generic rules for the scenario tests: profile cards with name, age, city and bio.</summary>
    public static ParsingOptions Parsing() => new()
    {
        LikeMarkers = ["понравил", "лайкнул", "yoqtir", "liked you"],
        MutualMarkers = ["взаимн", "o'zaro", "o‘zaro", "mutual", "it's a match"],
        IgnoreMarkers = ["Лайк отправлен"],
        ProfilePatterns = [FullProfilePattern],
        InterestsPattern = @"^(?:Интересы|Увлечения|Qiziqishlar|Interests)\s*:\s*(?<interests>.+)$",
        UsernamePattern = UsernamePattern,
        PersonIdPattern = string.Empty,
        UnknownMessages = UnknownMessagePolicy.ParseFailed,
    };

    /// <summary>The shipped rules for @Znakomstva_Tashkentt_bot: a match card carries only the name and @username.</summary>
    public static ParsingOptions TashdateParsing() => new()
    {
        LikeMarkers = ["понравил", "лайкнул", "yoqtir", "liked you"],
        MutualMarkers = ["у вас мэтч", "мэтч с", "взаимн", "o'zaro", "o‘zaro", "mutual", "it's a match"],
        LikeHintMarkers = ["Тобой интересуются", "тебя лайкнули"],
        ProfilePatterns = [@"мэтч с (?<name>[^!\n]{1,64})!", FullProfilePattern],
        InterestsPattern = @"^(?:Интересы|Увлечения|Qiziqishlar|Interests)\s*:\s*(?<interests>.+)$",
        UsernamePattern = UsernamePattern,
        PersonIdPattern = @"@(?<id>[A-Za-z][A-Za-z0-9_]{4,31})",
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

    /// <summary>
    /// The shipped setup for Tashdate: drafts wait for you, who picks Telegram or the in-app chat. App likes are liked
    /// back without a card unless <paramref name="likeBack"/> asks for one: most tests are about what follows the like.
    /// </summary>
    public static ProcessingOptions TashdateProcessing(
        bool miniApp = true, ProcessingMode mode = ProcessingMode.Confirm, LikeBackMode likeBack = LikeBackMode.Auto) => new()
        {
            Mode = mode,
            LikedMeMessage = LikedMeMessagePolicy.AfterLikeBack,
            MutualRoute = MessageRouteKind.DirectToUser,
            Channels = [MessageChannel.Telegram, MessageChannel.App],
            MiniApp = new MiniAppOptions { Enabled = miniApp, LikeBack = likeBack, MaxLikesPerPass = 10 },
            LikeBackButtons = ["❤"],
            MinPause = TimeSpan.FromSeconds(5),
            MaxPause = TimeSpan.FromSeconds(30),
            MaxActionsPerHour = 20,
            HistoryBatchSize = 10,
        };

    public static ReplyOptions Replies(InvalidReplyPolicy policy = InvalidReplyPolicy.Template, int maxPhotos = 10) => new()
    {
        Owner = new OwnerProfileOptions { Name = "Рустам", About = "разработчик", Tone = "дружелюбный", Avoid = "работа" },
        DefaultLanguage = ReplyLanguage.Russian,
        MinLength = 20,
        MaxLength = 400,
        MaxEmoji = 1,
        MaxPhotos = maxPhotos,
        InvalidReplyPolicy = policy,
        Templates = new()
        {
            ["Russian"] = ["Привет, {name}! Приятно, что мы совпали. Как проходит твой день?"],
            ["Uzbek"] = ["Salom, {name}! Tanishganimizdan xursandman."],
            ["English"] = ["Hi {name}! Glad we matched."],
        },
    };
}
