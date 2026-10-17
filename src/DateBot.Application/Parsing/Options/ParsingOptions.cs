using DateBot.Application.Parsing.Enums;

namespace DateBot.Application.Parsing.Options;

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

    /// <summary>"Someone liked you" without a profile; the likes are then read from the app.</summary>
    public string[] LikeHintMarkers { get; init; } = [];

    /// <summary>
    /// Regexes with groups <c>name</c> (required), <c>age</c>, <c>city</c>, <c>bio</c>; tried in order, the first match
    /// wins. Several are needed because notifications differ: a match card shows only the name.
    /// </summary>
    public string[] ProfilePatterns { get; init; } = [];

    /// <summary>Regex with group <c>interests</c>; items are split by comma, semicolon, # or bullet.</summary>
    public string InterestsPattern { get; init; } = string.Empty;

    /// <summary>Regex with group <c>username</c>.</summary>
    public string UsernamePattern { get; init; } = string.Empty;

    /// <summary>Regex with group <c>id</c> when the bot shows a stable profile id; empty = profile fingerprint.</summary>
    public string PersonIdPattern { get; init; } = string.Empty;

    public UnknownMessagePolicy UnknownMessages { get; init; } = UnknownMessagePolicy.ParseFailed;
}
