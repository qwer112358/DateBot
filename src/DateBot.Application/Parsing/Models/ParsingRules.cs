using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using DateBot.Application.Parsing.Enums;
using DateBot.Application.Parsing.Options;

namespace DateBot.Application.Parsing.Models;

/// <summary>Compiled, immutable form of <see cref="ParsingOptions"/>.</summary>
public sealed record ParsingRules(
    ImmutableArray<string> LikeMarkers,
    ImmutableArray<string> MutualMarkers,
    ImmutableArray<string> IgnoreMarkers,
    ImmutableArray<Regex> Profiles,
    Maybe<Regex> Interests,
    Maybe<Regex> Username,
    Maybe<Regex> PersonId,
    UnknownMessagePolicy UnknownMessages)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public ImmutableArray<string> LikeHintMarkers { get; init; } = [];

    public static ParsingRules From(ParsingOptions options) => new(
        Clean(options.LikeMarkers),
        Clean(options.MutualMarkers),
        Clean(options.IgnoreMarkers),
        [.. options.ProfilePatterns.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Compile)],
        Optional(options.InterestsPattern),
        Optional(options.UsernamePattern),
        Optional(options.PersonIdPattern),
        options.UnknownMessages)
    {
        LikeHintMarkers = Clean(options.LikeHintMarkers),
    };

    private static ImmutableArray<string> Clean(IEnumerable<string> markers) =>
        [.. markers.Select(m => m.Trim()).Where(m => m.Length > 0)];

    private static Maybe<Regex> Optional(string pattern) =>
        string.IsNullOrWhiteSpace(pattern) ? Maybe<Regex>.None : Maybe.From(Compile(pattern));

    private static Regex Compile(string pattern) =>
        new(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase, RegexTimeout);
}
