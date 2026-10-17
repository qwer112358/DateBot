using System.Collections.Immutable;
using System.Globalization;

namespace DateBot.Domain.Matches;

/// <summary>
/// Every key a person can be recognised by. One person may reach us through different doors (a like in the app,
/// a match card in the bot chat), so tombstones and lookups use all of them.
/// </summary>
public static class PersonKeys
{
    public static string ForApp(long appProfileId) => "app:" + appProfileId.ToString(CultureInfo.InvariantCulture);

    public static string ForTelegram(string username) => "tg:" + username.TrimStart('@').ToLowerInvariant();

    public static ImmutableArray<string> Of(string personId, string? username, long? appProfileId)
    {
        var keys = ImmutableArray.CreateBuilder<string>(3);
        keys.Add(personId);
        if (!string.IsNullOrWhiteSpace(username))
        {
            keys.Add(ForTelegram(username));
        }

        if (appProfileId is { } appId)
        {
            keys.Add(ForApp(appId));
        }

        return [.. keys.Distinct(StringComparer.Ordinal)];
    }

    public static ImmutableArray<string> Of(TashdateUserId personId, Profile profile) =>
        Of(
            personId.Value,
            profile.Username.HasValue ? profile.Username.GetValueOrThrow().Value : null,
            profile.AppId.HasValue ? profile.AppId.GetValueOrThrow().Value : null);
}
