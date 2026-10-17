using System.Collections.Frozen;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace DateBot.Infrastructure.MiniApp;

/// <summary>The app's own labels for its codes (<c>/api/i18n/{lang}</c>): "interest.football" → "Футбол".</summary>
internal sealed class AppTranslations(FrozenDictionary<string, string> strings)
{
    public static AppTranslations Empty { get; } = new(FrozenDictionary<string, string>.Empty);

    /// <summary>The label, or <paramref name="fallback"/> when the app has none.</summary>
    public string Label(string key, string fallback) =>
        strings.TryGetValue(key, out var label) && !string.IsNullOrWhiteSpace(label) ? label.Trim() : fallback;

    /// <summary>Reads <c>{"lang": "ru", "strings": {...}}</c>; anything else gives no labels.</summary>
    public static AppTranslations From(JsonElement body) =>
        AppJson.Object(body, "strings")
            .Map(strings => new AppTranslations(strings.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToFrozenDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal)))
            .GetValueOrDefault(Empty);
}
