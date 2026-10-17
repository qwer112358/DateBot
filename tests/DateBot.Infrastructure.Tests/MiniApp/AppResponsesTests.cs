using System.Collections.Frozen;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp.Models;
using DateBot.Infrastructure.MiniApp.Models;
using DateBot.Infrastructure.MiniApp.Readers;

namespace DateBot.Infrastructure.Tests.MiniApp;

/// <summary>The app's JSON as the Mini App receives it (fictional people): codes in strings, lists inside strings.</summary>
public sealed class AppResponsesTests
{
    private static readonly AppTranslations Labels = new(new Dictionary<string, string>
    {
        ["interest.football"] = "Футбол",
        ["interest.coffee"] = "Кофе",
        ["city.samarkand"] = "Самарканд",
        ["reg.goal_serious"] = "Серьёзные отношения",
        ["profile.education_title"] = "Образование",
        ["education.higher"] = "Высшее",
        ["card.height"] = "Рост",
        ["card.cm"] = "см",
        ["card.language"] = "Язык общения",
        ["reg.lang_uz"] = "Узбекский",
        ["profile.pets_title"] = "Питомцы",
        ["pets.cat"] = "Кошка",
        ["card.smoking"] = "Курение",
        ["card.alcohol"] = "Алкоголь",
        ["habits.none"] = "Нет",
    }.ToFrozenDictionary());

    [Fact]
    public void Profile_codes_become_the_app_labels_and_facts()
    {
        var profile = AppResponses.Profile(Json("""
            {
              "id": 501, "name": "Лола", "age": 23, "city": "samarkand",
              "bio": "Люблю джаз и рамен",
              "interests": "[\"football\", \"coffee\", \"unknown_code\"]",
              "height": 165,
              "profile_extras": "{\"education\": \"higher\", \"pets\": \"cat\", \"habits\": {\"none\": true}}",
              "communication_language": "uz",
              "dating_goal": "serious",
              "photos": "[\"a1.jpg\", \"a2.jpg\"]"
            }
            """), Labels).Value;

        profile.Id.Should().Be(501);
        profile.Age.Should().Be(Maybe.From(23));
        profile.City.Should().Be(Maybe.From("Самарканд"));
        profile.Interests.Should().Equal("Футбол", "Кофе", "unknown_code");
        profile.Facts.Should().Equal(
            "Цель знакомства: Серьёзные отношения",
            "Образование: Высшее",
            "Рост: 165 см",
            "Язык общения: Узбекский",
            "Курение: Нет",
            "Алкоголь: Нет",
            "Питомцы: Кошка");
        profile.PhotoFiles.Should().Equal("a1.jpg", "a2.jpg");
        profile.RawText.Should().StartWith("Лола, 23, Самарканд\nЛюблю джаз и рамен");
    }

    [Fact]
    public void Empty_profile_is_still_a_profile()
    {
        var profile = AppResponses.Profile(Json("""
            {"id": "77", "name": "Мадина", "bio": null, "interests": null, "profile_extras": "{}", "photos": "[]", "height": 20}
            """), AppTranslations.Empty).Value;

        profile.Id.Should().Be(77);
        profile.Bio.HasNoValue.Should().BeTrue();
        profile.Interests.Should().BeEmpty();
        profile.Facts.Should().BeEmpty("an impossible height is dropped");
        profile.PhotoFiles.Should().BeEmpty();
    }

    [Fact]
    public void Profile_without_id_is_a_bad_response() =>
        AppResponses.Profile(Json("""{"name": "x"}"""), Labels).Error.Code.Should().Be("miniapp.bad_response");

    [Fact]
    public void Likes_without_an_id_or_locked_are_hidden()
    {
        var (open, hidden) = AppResponses.Likes(Json("""
            [
              {"id": 501, "name": "Лола", "locked": false, "has_gift": "0"},
              {"id": null, "age": 25, "locked": true},
              {"id": 502, "name": "Мадина", "locked": true}
            ]
            """));

        open.Should().ContainSingle().Which.Should().Be(new AppLike(501, "Лола"));
        hidden.Should().Be(2);
    }

    [Fact]
    public void Matches_and_like_result_carry_the_username_without_at_sign()
    {
        AppResponses.Matches(Json("""[{"id": 501, "name": "Лола", "username": "@lola_tash"}, {"id": 502, "name": "Мадина", "username": null}]"""))
            .Select(m => (m.ProfileId, m.Username.GetValueOrDefault("-")))
            .Should().Equal((501L, "lola_tash"), (502L, "-"));

        var like = AppResponses.LikeResult(Json("""{"match": true, "username": "lola_tash", "ad": null}"""));
        like.Mutual.Should().BeTrue();
        like.Username.Should().Be(Maybe.From("lola_tash"));
        AppResponses.LikeResult(Json("""{"match": false}""")).Mutual.Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"detail": {"code": "chat_unavailable"}}""", "chat_unavailable")]
    [InlineData("""{"error": "like_limit_reached", "limit": 30}""", "like_limit_reached")]
    [InlineData("""{"detail": "Invalid init data"}""", null)]
    public void Error_code_is_read_from_either_shape(string body, string? code) =>
        AppResponses.ErrorCode(Json(body)).GetValueOrDefault(null!).Should().Be(code);

    [Fact]
    public void Translations_come_from_the_strings_object()
    {
        var labels = AppTranslations.From(Json("""{"lang": "ru", "strings": {"pets.cat": "Кошка", "n": 1}}"""));

        labels.Label("pets.cat", "cat").Should().Be("Кошка");
        labels.Label("pets.dog", "dog").Should().Be("dog");
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
