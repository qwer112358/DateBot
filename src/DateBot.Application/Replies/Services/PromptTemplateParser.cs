using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using DateBot.Application.Replies.Models;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Replies.Services;

/// <summary>
/// Pure: the prompts file → its sections. A section starts with a <c>## name</c> line; text above the first one is a
/// comment. A missing section, an unknown placeholder (a typo) or a revision without <c>{hint}</c> is an error.
/// </summary>
public static partial class PromptTemplateParser
{
    public const string Hint = "hint";

    /// <summary>Every placeholder <see cref="PromptBuilder"/> fills.</summary>
    public static readonly FrozenSet<string> Placeholders = new[]
    {
        "owner_name", "owner_about", "max_length", "language", "photo_rule", Hint,
    }.ToFrozenSet();

    private static readonly string[] Sections = ["system", "photo", "no_photo", "first_message", "revision_system", "revision"];

    public static Result<PromptTemplates, Error> Parse(string text)
    {
        var sections = Split(text);
        var missing = Sections.Where(name => !sections.ContainsKey(name) || sections[name].Length == 0).ToArray();
        if (missing.Length > 0)
        {
            return Invalid($"нет раздела или он пустой: {string.Join(", ", missing.Select(m => "## " + m))}");
        }

        var unknown = sections.Values
            .SelectMany(section => PlaceholderPattern().Matches(section).Select(m => m.Groups["name"].Value))
            .Where(name => !Placeholders.Contains(name))
            .Distinct()
            .ToArray();
        if (unknown.Length > 0)
        {
            return Invalid($"неизвестные подстановки: {string.Join(", ", unknown.Select(u => "{" + u + "}"))}");
        }

        if (!sections["revision"].Contains("{" + Hint + "}", StringComparison.Ordinal))
        {
            return Invalid("в разделе revision нет {hint}: ваше пожелание не дойдёт до модели");
        }

        return new PromptTemplates(
            sections["system"], sections["photo"], sections["no_photo"], sections["first_message"], sections["revision_system"], sections["revision"]);
    }

    [GeneratedRegex(@"\{(?<name>[a-z_]+)\}")]
    public static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"^##\s+(?<name>[a-z_]+)\s*$")]
    private static partial Regex HeaderPattern();

    private static Dictionary<string, string> Split(string text)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        string? current = null;
        var lines = new List<string>();
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var header = HeaderPattern().Match(line);
            if (header.Success)
            {
                Close();
                current = header.Groups["name"].Value;
                continue;
            }

            lines.Add(line);
        }

        Close();
        return sections;

        void Close()
        {
            if (current is not null)
            {
                sections[current] = string.Join('\n', lines).Trim('\n', ' ');
            }

            lines.Clear();
        }
    }

    private static Error Invalid(string reason) => Error.Validation("prompts.invalid", $"Файл промптов: {reason}");
}
