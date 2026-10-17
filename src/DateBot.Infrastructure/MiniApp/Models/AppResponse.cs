using System.Net;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace DateBot.Infrastructure.MiniApp.Models;

/// <summary>An HTTP answer of the app, read completely, so no stream outlives the request.</summary>
internal sealed record AppResponse(HttpStatusCode Status, byte[] Body, string? MediaType, IReadOnlyDictionary<string, string> Headers)
{
    public bool IsSuccess => (int)Status is >= 200 and < 300;

    public Maybe<string> Header(string name) =>
        Headers.TryGetValue(name, out var value) ? value : Maybe<string>.None;

    /// <summary>The body as JSON; an empty or non-JSON body gives an undefined element.</summary>
    public JsonElement Json()
    {
        if (Body.Length == 0)
        {
            return default;
        }

        try
        {
            using var document = JsonDocument.Parse(Body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
