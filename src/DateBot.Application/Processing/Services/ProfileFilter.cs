using DateBot.Application.Processing.Options;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Processing.Services;

/// <summary>Pure filter over a profile. With filters disabled every profile passes.</summary>
public static class ProfileFilter
{
    public static bool Accepts(Profile profile, FilterOptions filters) =>
        !filters.Enabled
        || (AgeFits(profile, filters) && CityFits(profile, filters) && !HasStopWord(profile, filters));

    private static bool AgeFits(Profile profile, FilterOptions filters) =>
        profile.Age.HasNoValue
        || profile.Age.GetValueOrThrow().Value is var age && age >= filters.MinAge && age <= filters.MaxAge;

    private static bool CityFits(Profile profile, FilterOptions filters) =>
        filters.Cities.Length == 0
        || (profile.City.HasValue
            && filters.Cities.Any(c => string.Equals(c.Trim(), profile.City.GetValueOrThrow().Value, StringComparison.OrdinalIgnoreCase)));

    private static bool HasStopWord(Profile profile, FilterOptions filters) =>
        filters.StopWords.Any(w => w.Length > 0 && profile.RawText.Contains(w, StringComparison.OrdinalIgnoreCase));
}
