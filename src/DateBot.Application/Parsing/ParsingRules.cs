using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using FluentValidation;

namespace DateBot.Application.Parsing;

/// <summary>Compiled, immutable form of <see cref="ParsingOptions"/>.</summary>
public sealed record ParsingRules(
    ImmutableArray<string> LikeMarkers,
    ImmutableArray<string> MutualMarkers,
    ImmutableArray<string> IgnoreMarkers,
    Regex Profile,
    Maybe<Regex> Interests,
    Maybe<Regex> Username,
    Maybe<Regex> PersonId,
    UnknownMessagePolicy UnknownMessages)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static ParsingRules From(ParsingOptions options) => new(
        Clean(options.LikeMarkers),
        Clean(options.MutualMarkers),
        Clean(options.IgnoreMarkers),
        new Regex(options.ProfilePattern, RegexOptions.Multiline | RegexOptions.IgnoreCase, RegexTimeout),
        Optional(options.InterestsPattern),
        Optional(options.UsernamePattern),
        Optional(options.PersonIdPattern),
        options.UnknownMessages);

    private static ImmutableArray<string> Clean(IEnumerable<string> markers) =>
        [.. markers.Select(m => m.Trim()).Where(m => m.Length > 0)];

    private static Maybe<Regex> Optional(string pattern) =>
        string.IsNullOrWhiteSpace(pattern)
            ? Maybe<Regex>.None
            : Maybe.From(new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase, RegexTimeout));
}
