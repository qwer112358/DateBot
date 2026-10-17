using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Telegram;

namespace DateBot.Application.Tests;

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
