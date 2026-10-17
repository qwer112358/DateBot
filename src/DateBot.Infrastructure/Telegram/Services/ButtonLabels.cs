using CSharpFunctionalExtensions;

namespace DateBot.Infrastructure.Telegram.Services;

/// <summary>
/// Matches bot button captions against configured labels. A label must be a whole word of the caption
/// ("Лайк" matches "❤️ Лайк" but not "Дизлайк"), and labels are tried in the configured order.
/// </summary>
public static class ButtonLabels
{
    public static Maybe<T> Pick<T>(IEnumerable<T> buttons, Func<T, string> caption, IEnumerable<string> labels)
    {
        var candidates = buttons.ToList();
        return labels
            .Select(label => candidates.TryFirst(button => Matches(caption(button), label)))
            .TryFirst(found => found.HasValue)
            .GetValueOrDefault(Maybe<T>.None);
    }

    public static bool Matches(string caption, string label)
    {
        var text = Normalize(caption);
        var needle = Normalize(label);
        if (needle.Length == 0)
        {
            return false;
        }

        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + 1, StringComparison.Ordinal))
        {
            var end = index + needle.Length;
            var wordStart = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var wordEnd = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (wordStart && wordEnd)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Drops the emoji variation selector so "❤️" and "❤" compare equal.</summary>
    private static string Normalize(string value) =>
        value.Replace("️", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
}
