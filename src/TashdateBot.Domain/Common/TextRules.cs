using System.Text.RegularExpressions;

namespace TashdateBot.Domain.Common;

/// <summary>Pure text checks shared by value objects and validators.</summary>
public static partial class TextRules
{
    public static bool ContainsLink(string text) => LinkPattern().IsMatch(text);

    public static bool ContainsPhone(string text) => PhonePattern().IsMatch(text);

    public static bool ContainsMention(string text) => MentionPattern().IsMatch(text);

    public static string NormalizeSpaces(string text) => SpacesPattern().Replace(text.Trim(), " ");

    [GeneratedRegex(@"(https?://|www\.|t\.me/|telegram\.me/|\b[\w-]+\.(com|ru|uz|org|net|io|me|app)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"(\+?\d[\d\s\-().]{7,}\d)")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(^|\s)@[A-Za-z][A-Za-z0-9_]{3,}")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpacesPattern();
}
