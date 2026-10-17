using CSharpFunctionalExtensions;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public static readonly BotMessageId Anchor = BotMessageId.Create(105).Value;

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

    /// <summary>A like found in the app: no bot message, identity by the app id.</summary>
    public static Match AppLike(long appId = 501, string name = "Лола") =>
        Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value,
            TashdateUserId.Create(PersonKeys.ForApp(appId)).Value,
            Maybe<BotMessageId>.None,
            MatchKind.LikedMe,
            Matches.Profile.Create(new ProfileInput(
                ProfileName.Create(name).Value,
                Maybe.From(Age.Create(23).Value),
                Maybe.From(City.Create("Ташкент").Value),
                Maybe<Bio>.None,
                Interests.Create(["кофе"]).Value,
                Maybe.From(PhotoFileId.Create("app:abc_1.jpg").Value),
                Maybe<ContactUsername>.None,
                $"{name}, 23, Ташкент",
                ProfileFacts.Create(["Рост: 165 см"]).Value,
                Maybe.From(AppProfileId.Create(appId).Value))).Value,
            Now)).Value;

    public static ReplyText Reply(string text = "Привет! Вижу, ты тоже любишь горы.") => ReplyText.Create(text).Value;
}
