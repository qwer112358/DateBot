using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Telegram;

namespace DateBot.Application.Tests;

/// <summary>
/// Anonymised examples of bot notifications. <see cref="TashdateMatch"/> copies the real match card of
/// @Znakomstva_Tashkentt_bot (photo + caption + "Открыть знакомства" web-app button); the others are synthetic.
/// </summary>
internal static class Samples
{
    /// <summary>Real format, fictional person.</summary>
    public static RawBotMessage TashdateMatch(long id, string name = "Аня", string username = "anya_sample") => new(
        id,
        SentAt,
        $"❤️ У вас мэтч с {name}!\n\nВы оба лайкнули друг друга. Самое время написать.\n@{username}",
        [new BotButton("💛 Открыть знакомства", 0, 0)],
        Maybe.From("photo:222"));

    /// <summary>Real format of a match with someone who has no Telegram username: the card ends without the @line.</summary>
    public static RawBotMessage TashdateMatchWithoutUsername(long id, string name = "Шахноза", string photo = "photo:333") => new(
        id,
        SentAt,
        $"❤️ У вас мэтч с {name}!\n\nВы оба лайкнули друг друга. Самое время написать.",
        [new BotButton("💛 Открыть знакомства", 0, 0)],
        Maybe.From(photo));

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
