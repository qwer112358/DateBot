using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using FluentValidation;

namespace DateBot.Application.Parsing;

public enum UnknownMessagePolicy
{
    /// <summary>Unrecognised messages go to ParseFailed and you get a notification.</summary>
    ParseFailed,

    /// <summary>Unrecognised messages are stored as Ignored without a notification.</summary>
    Ignore,
}

/// <summary>Recognition rules for the bot notifications. All of them live in configuration.</summary>
public sealed class ParsingOptions
{
    public const string Section = "Parsing";

    /// <summary>Text fragments that mark "someone liked you".</summary>
    public string[] LikeMarkers { get; init; } = [];

    /// <summary>Text fragments that mark a mutual like. Checked before <see cref="LikeMarkers"/>.</summary>
    public string[] MutualMarkers { get; init; } = [];

    /// <summary>Service messages of the bot that need no reaction.</summary>
    public string[] IgnoreMarkers { get; init; } = [];

    /// <summary>Regex with groups <c>name</c> (required), <c>age</c>, <c>city</c>, <c>bio</c>.</summary>
    public string ProfilePattern { get; init; } = string.Empty;

    /// <summary>Regex with group <c>interests</c>; items are split by comma, semicolon, # or bullet.</summary>
    public string InterestsPattern { get; init; } = string.Empty;

    /// <summary>Regex with group <c>username</c>.</summary>
    public string UsernamePattern { get; init; } = string.Empty;

    /// <summary>Regex with group <c>id</c> when the bot shows a stable profile id; empty = profile fingerprint.</summary>
    public string PersonIdPattern { get; init; } = string.Empty;

    public UnknownMessagePolicy UnknownMessages { get; init; } = UnknownMessagePolicy.ParseFailed;
}

public sealed class ParsingOptionsValidator : AbstractValidator<ParsingOptions>
{
    public ParsingOptionsValidator()
    {
        RuleFor(o => o.LikeMarkers).NotEmpty();
        RuleFor(o => o.MutualMarkers).NotEmpty();
        RuleFor(o => o.ProfilePattern)
            .NotEmpty()
            .Must(p => IsRegex(p) && p.Contains("?<name>", StringComparison.Ordinal))
            .WithMessage("ProfilePattern должен быть корректным regex с группой name");
        RuleFor(o => o.InterestsPattern).Must(p => p.Length == 0 || IsRegex(p)).WithMessage("InterestsPattern: некорректный regex");
        RuleFor(o => o.UsernamePattern).Must(p => p.Length == 0 || IsRegex(p)).WithMessage("UsernamePattern: некорректный regex");
        RuleFor(o => o.PersonIdPattern).Must(p => p.Length == 0 || IsRegex(p)).WithMessage("PersonIdPattern: некорректный regex");
        RuleFor(o => o.UnknownMessages).IsInEnum();
    }

    private static bool IsRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

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
