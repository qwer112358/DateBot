using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using DateBot.Application.Telegram;
using DateBot.Domain.Matches;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace DateBot.Application.Parsing;

/// <summary>Pure function: bot message + rules → typed notification. Never throws on bad input.</summary>
public static class BotNotificationParser
{
    private static readonly char[] InterestSeparators = [',', ';', '#', '•', '|'];

    public static BotNotification Parse(RawBotMessage message, ParsingRules rules)
    {
        var text = message.Text;

        if (ContainsAny(text, rules.IgnoreMarkers))
        {
            return new IgnoredNotification(message, "Служебное сообщение бота");
        }

        var kind = DetectKind(text, rules);
        if (kind.HasNoValue)
        {
            const string reason = "Нет признаков лайка или взаимной симпатии";
            return rules.UnknownMessages == UnknownMessagePolicy.Ignore
                ? new IgnoredNotification(message, reason)
                : new UnknownNotification(message, reason);
        }

        var profile = ParseProfile(message, rules);
        if (profile.IsFailure)
        {
            return new UnknownNotification(message, profile.Error);
        }

        return kind.GetValueOrThrow() == MatchKind.Mutual
            ? new MutualNotification(message, profile.Value)
            : new LikeNotification(message, profile.Value);
    }

    private static Maybe<MatchKind> DetectKind(string text, ParsingRules rules) =>
        ContainsAny(text, rules.MutualMarkers) ? MatchKind.Mutual
        : ContainsAny(text, rules.LikeMarkers) ? MatchKind.LikedMe
        : Maybe<MatchKind>.None;

    private static Result<ParsedProfile, string> ParseProfile(RawBotMessage message, ParsingRules rules)
    {
        var match = rules.Profiles
            .Select(pattern => SafeMatch(pattern, message.Text))
            .TryFirst(found => found.HasValue)
            .GetValueOrDefault(Maybe<RegexMatch>.None);
        if (match.HasNoValue)
        {
            return Result.Failure<ParsedProfile, string>("Не удалось найти в сообщении анкету: ни один шаблон из Parsing:ProfilePatterns не подошёл");
        }

        var groups = match.GetValueOrThrow().Groups;
        var interestsLine = rules.Interests.Bind(r => SafeMatch(r, message.Text));

        return new ParsedProfile(
            Name: groups["name"].Value.Trim(),
            Age: Group(groups, "age").Bind(ParseInt),
            City: Group(groups, "city"),
            Bio: Group(groups, "bio").Map(bio => CleanBio(bio, rules, interestsLine)).Where(bio => bio.Length > 0),
            Interests: interestsLine.Map(SplitInterests).GetValueOrDefault(ImmutableArray<string>.Empty),
            PhotoId: message.PhotoId,
            Username: rules.Username.Bind(r => SafeMatch(r, message.Text)).Bind(m => Group(m.Groups, "username")),
            PersonId: rules.PersonId.Bind(r => SafeMatch(r, message.Text)).Bind(m => Group(m.Groups, "id")));
    }

    /// <summary>Removes the interests line and marker lines that the bio group may have swallowed.</summary>
    private static string CleanBio(string bio, ParsingRules rules, Maybe<RegexMatch> interestsLine)
    {
        var withoutInterests = interestsLine.HasValue ? bio.Replace(interestsLine.GetValueOrThrow().Value, string.Empty, StringComparison.Ordinal) : bio;
        var markers = rules.LikeMarkers.AddRange(rules.MutualMarkers);
        var lines = withoutInterests
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !ContainsAny(l, markers));
        return string.Join("\n", lines).Trim();
    }

    private static ImmutableArray<string> SplitInterests(RegexMatch line) =>
        Group(line.Groups, "interests")
            .Map(v => v.Split(InterestSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray())
            .GetValueOrDefault(ImmutableArray<string>.Empty);

    private static Maybe<string> Group(GroupCollection groups, string name) =>
        groups[name] is { Success: true } group && group.Value.Trim().Length > 0
            ? Maybe.From(group.Value.Trim())
            : Maybe<string>.None;

    private static Maybe<int> ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : Maybe<int>.None;

    private static Maybe<RegexMatch> SafeMatch(Regex regex, string text)
    {
        try
        {
            var match = regex.Match(text);
            return match.Success ? match : Maybe<RegexMatch>.None;
        }
        catch (RegexMatchTimeoutException)
        {
            return Maybe<RegexMatch>.None;
        }
    }

    private static bool ContainsAny(string text, ImmutableArray<string> markers) =>
        markers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
}
