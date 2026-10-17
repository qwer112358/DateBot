using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Infrastructure.MiniApp;

/// <summary>
/// Pure: the app's JSON → application types. Codes (interests, pets, city…) become the app's own labels; the
/// structured fields become fact lines in the same order the app shows them in an opened profile.
/// </summary>
internal static class AppResponses
{
    private static readonly string[] CityCodes =
        ["tashkent", "nukus", "andijan", "bukhara", "jizzakh", "qarshi", "navoi", "namangan", "samarkand", "termez", "gulistan", "nurafshon", "fergana", "urgench"];

    /// <summary><c>GET /api/user/{telegram_id}</c> → your profile id in the app.</summary>
    public static Result<long, Error> OwnId(JsonElement user) =>
        AppJson.Long(user, "id").ToResult(AppErrors.BadResponse("в профиле нет id"));

    /// <summary>One page of <c>GET /api/likes/{me}</c>: open likes carry an id, hidden ones (premium) do not.</summary>
    public static (ImmutableArray<AppLike> Open, int Hidden) Likes(JsonElement page)
    {
        if (page.ValueKind != JsonValueKind.Array)
        {
            return ([], 0);
        }

        var open = ImmutableArray.CreateBuilder<AppLike>();
        var hidden = 0;
        foreach (var like in page.EnumerateArray())
        {
            var id = AppJson.Long(like, "id");
            if (id.HasNoValue || AppJson.Flag(like, "locked"))
            {
                hidden++;
                continue;
            }

            open.Add(new AppLike(id.GetValueOrThrow(), AppJson.String(like, "name").GetValueOrDefault("?")));
        }

        return (open.ToImmutable(), hidden);
    }

    /// <summary><c>GET /api/matches/{me}</c>.</summary>
    public static ImmutableArray<AppMatch> Matches(JsonElement list) =>
        list.ValueKind != JsonValueKind.Array
            ? []
            : [.. list.EnumerateArray()
                .Select(m => (Id: AppJson.Long(m, "id"), Match: m))
                .Where(m => m.Id.HasValue)
                .Select(m => new AppMatch(
                    m.Id.GetValueOrThrow(),
                    AppJson.String(m.Match, "name").GetValueOrDefault(string.Empty),
                    AppJson.String(m.Match, "username").Map(u => u.TrimStart('@'))))];

    /// <summary><c>POST /api/like</c> → <c>{match, username}</c>.</summary>
    public static AppLikeResult LikeResult(JsonElement body) =>
        new(AppJson.Flag(body, "match"), AppJson.String(body, "username").Map(u => u.TrimStart('@')));

    /// <summary>Error code of a failed request: <c>{"detail": {"code": …}}</c> or <c>{"error": …}</c>.</summary>
    public static Maybe<string> ErrorCode(JsonElement body) =>
        AppJson.Object(body, "detail").Bind(detail => AppJson.String(detail, "code"))
            .Or(() => AppJson.String(body, "error"));

    /// <summary><c>GET /api/user/id/{id}</c>.</summary>
    public static Result<AppProfile, Error> Profile(JsonElement p, AppTranslations t)
    {
        var id = AppJson.Long(p, "id");
        var name = AppJson.String(p, "name");
        if (id.HasNoValue || name.HasNoValue)
        {
            return AppErrors.BadResponse("в анкете нет id или имени");
        }

        var age = AppJson.Long(p, "age").Where(a => a is >= Age.Min and <= Age.Max).Map(a => (int)a);
        var city = AppJson.String(p, "city").Map(code => CityName(code, t)).Map(c => Truncate(c, City.MaxLength));
        var bio = AppJson.String(p, "bio").Map(b => Truncate(b, Bio.MaxLength));
        var interests = AppJson.Strings(p, "interests")
            .Select(code => Truncate(t.Label("interest." + code, code), Interests.MaxItemLength))
            .Take(Interests.MaxCount)
            .ToImmutableArray();
        var facts = Facts(p, t).Select(f => Truncate(f, ProfileFacts.MaxItemLength)).Take(ProfileFacts.MaxCount).ToImmutableArray();
        var photo = AppJson.Strings(p, "photos").TryFirst();

        return new AppProfile(
            id.GetValueOrThrow(),
            name.GetValueOrThrow(),
            age,
            city,
            bio,
            interests,
            facts,
            photo,
            RawText(name.GetValueOrThrow(), age, city, bio, interests, facts));
    }

    private static IEnumerable<string> Facts(JsonElement p, AppTranslations t)
    {
        var extras = AppJson.Object(p, "profile_extras");
        string? Extra(string field) => extras.Bind(e => AppJson.String(e, field)).GetValueOrDefault(null!);
        string Line(string titleKey, string titleFallback, string value) => $"{t.Label(titleKey, titleFallback)}: {value}";

        if (AppJson.String(p, "dating_goal").TryGetValue(out var goal))
        {
            yield return Line("profile.goal_title", "Цель знакомства", t.Label("reg.goal_" + goal, goal));
        }

        if (Extra("education") is { } education)
        {
            yield return Line("profile.education_title", "Образование", t.Label("education." + education, education));
        }

        if (AppJson.Long(p, "height").Where(h => h is >= 100 and <= 250).TryGetValue(out var height))
        {
            yield return Line("card.height", "Рост", $"{height.ToString(CultureInfo.InvariantCulture)} {t.Label("card.cm", "см")}");
        }

        if (AppJson.String(p, "communication_language").Map(l => l.ToLowerInvariant()).Where(l => l is "ru" or "uz" or "both").TryGetValue(out var language))
        {
            yield return Line("card.language", "Язык общения", t.Label("reg.lang_" + language, language));
        }

        if (Extra("sport") is { } sport)
        {
            yield return Line("profile.sport_title", "Спорт", t.Label("sport." + sport, sport));
        }

        foreach (var habit in Habits(extras, t))
        {
            yield return habit;
        }

        if (Extra("pets") is { } pets)
        {
            yield return Line("profile.pets_title", "Питомцы", t.Label("pets." + pets, pets));
        }

        if (Extra("family_plans") is { } family)
        {
            yield return Line("profile.family_plans_title", "Планы на семью", t.Label("family." + family, family));
        }

        if (Extra("comm_style") is { } style)
        {
            yield return Line("profile.comm_style_title", "Стиль общения", t.Label("comm." + style, style));
        }
    }

    private static IEnumerable<string> Habits(Maybe<JsonElement> extras, AppTranslations t)
    {
        if (!extras.Bind(e => AppJson.Object(e, "habits")).TryGetValue(out var habits))
        {
            yield break;
        }

        var smoking = t.Label("card.smoking", "Курение");
        var alcohol = t.Label("card.alcohol", "Алкоголь");
        if (AppJson.Flag(habits, "none"))
        {
            var none = t.Label("habits.none", "нет");
            yield return $"{smoking}: {none}";
            yield return $"{alcohol}: {none}";
            yield break;
        }

        if (AppJson.String(habits, "smoke").TryGetValue(out var smoke))
        {
            yield return $"{smoking}: {t.Label("smoke." + smoke, smoke)}";
        }

        if (AppJson.String(habits, "drink").TryGetValue(out var drink))
        {
            yield return $"{alcohol}: {t.Label("drink." + drink, drink)}";
        }
    }

    private static string CityName(string code, AppTranslations t) =>
        CityCodes.Contains(code.ToLowerInvariant()) ? t.Label("city." + code.ToLowerInvariant(), code) : code;

    private static string RawText(
        string name, Maybe<int> age, Maybe<string> city, Maybe<string> bio, ImmutableArray<string> interests, ImmutableArray<string> facts)
    {
        var header = string.Join(", ", new[] { name, age.Map(a => a.ToString(CultureInfo.InvariantCulture)).GetValueOrDefault(string.Empty), city.GetValueOrDefault(string.Empty) }
            .Where(part => part.Length > 0));
        var text = new StringBuilder(header);
        if (bio.HasValue)
        {
            text.Append('\n').Append(bio.GetValueOrThrow());
        }

        if (!interests.IsEmpty)
        {
            text.Append("\nИнтересы: ").Append(string.Join(", ", interests));
        }

        foreach (var fact in facts)
        {
            text.Append('\n').Append(fact);
        }

        return Truncate(text.ToString(), Domain.Matches.Profile.MaxRawTextLength);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
