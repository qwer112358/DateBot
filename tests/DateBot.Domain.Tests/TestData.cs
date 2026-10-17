using CSharpFunctionalExtensions;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public static Profile Profile(string name = "Анна", bool withUsername = false) =>
        Matches.Profile.Create(new ProfileInput(
            ProfileName.Create(name).Value,
            Maybe.From(Age.Create(24).Value),
            Maybe.From(City.Create("Ташкент").Value),
            Maybe.From(Bio.Create("Люблю горы и кофе").Value),
            Interests.Create(["горы", "кофе"]).Value,
            Maybe<PhotoFileId>.None,
            withUsername ? Maybe.From(ContactUsername.Create("anna_tash").Value) : Maybe<ContactUsername>.None,
            $"{name}, 24, Ташкент – Люблю горы и кофе")).Value;

    public static Match NewMatch(MatchKind kind, long messageId = 100) =>
        Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value,
            TashdateUserId.Create("fp:anna").Value,
            BotMessageId.Create(messageId).Value,
            kind,
            Profile(),
            Now)).Value;

    public static ReplyText Reply(string text = "Привет! Вижу, ты тоже любишь горы.") => ReplyText.Create(text).Value;
}
