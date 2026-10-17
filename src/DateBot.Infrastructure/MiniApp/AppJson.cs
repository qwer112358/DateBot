using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace DateBot.Infrastructure.MiniApp;

/// <summary>
/// Tolerant readers for the app's JSON: numbers may come as strings, lists and objects as JSON inside a string,
/// flags as 0/1. A missing or odd field reads as "none", never as an exception.
/// </summary>
internal static class AppJson
{
    public static Maybe<JsonElement> Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value
            : Maybe<JsonElement>.None;

    public static Maybe<long> Long(JsonElement element, string name) =>
        Property(element, name).Bind(value => value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => Maybe.From(number),
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => Maybe<long>.None,
        });

    public static Maybe<string> String(JsonElement element, string name) =>
        Property(element, name).Bind(value => value.ValueKind switch
        {
            JsonValueKind.String => Maybe.From(value.GetString()!.Trim()),
            JsonValueKind.Number => Maybe.From(value.GetRawText()),
            _ => Maybe<string>.None,
        }).Where(text => text.Length > 0);

    public static bool Flag(JsonElement element, string name) =>
        Property(element, name).Map(value => value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetInt64(out var number) && number != 0,
            JsonValueKind.String => value.GetString() is "1" or "true" or "True",
            _ => false,
        }).GetValueOrDefault(false);

    /// <summary>A JSON array of strings, or the same array serialised into a string.</summary>
    public static ImmutableArray<string> Strings(JsonElement element, string name) =>
        Embedded(element, name, JsonValueKind.Array)
            .Map(array => array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!.Trim())
                .Where(item => item.Length > 0)
                .ToImmutableArray())
            .GetValueOrDefault(ImmutableArray<string>.Empty);

    /// <summary>A JSON object, or the same object serialised into a string.</summary>
    public static Maybe<JsonElement> Object(JsonElement element, string name) => Embedded(element, name, JsonValueKind.Object);

    private static Maybe<JsonElement> Embedded(JsonElement element, string name, JsonValueKind kind) =>
        Property(element, name).Bind(value =>
            value.ValueKind == kind ? Maybe.From(value)
            : value.ValueKind == JsonValueKind.String ? Parse(value.GetString()!, kind)
            : Maybe<JsonElement>.None);

    private static Maybe<JsonElement> Parse(string json, JsonValueKind kind)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == kind ? document.RootElement.Clone() : Maybe<JsonElement>.None;
        }
        catch (JsonException)
        {
            return Maybe<JsonElement>.None;
        }
    }
}
