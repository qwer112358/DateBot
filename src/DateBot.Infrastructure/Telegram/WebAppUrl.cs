using CSharpFunctionalExtensions;

namespace DateBot.Infrastructure.Telegram;

/// <summary>Pure: the URL Telegram returns for a Mini App carries the signed init data in its fragment.</summary>
internal static class WebAppUrl
{
    private const string InitDataParameter = "tgWebAppData";

    public static Maybe<string> InitData(Uri url) =>
        url.Fragment.TrimStart('#')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .TryFirst(pair => pair.Length == 2 && pair[0] == InitDataParameter)
            .Map(pair => Uri.UnescapeDataString(pair[1]))
            .Where(value => value.Length > 0);

    public static Uri Origin(Uri url) => new(url.GetLeftPart(UriPartial.Authority) + "/");
}
