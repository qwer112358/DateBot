using System.Globalization;
using System.Text;
using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Models;
using DateBot.Application.Replies.Options;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Replies.Services;

/// <summary>
/// Pure prompt construction from the editable <see cref="PromptTemplates"/>. The profile is written by a stranger,
/// so code, not the templates, wraps it in tags as data, with angle brackets neutralised so it cannot close the tag.
/// </summary>
public static class PromptBuilder
{
    public static ReplyPrompt Build(
        PromptTemplates templates, Profile profile, OwnerProfileOptions owner, ReplyLanguage language, bool hasPhoto, int maxLength)
    {
        var user = new StringBuilder(ProfileBlock(profile))
            .AppendLine()
            .Append(templates.FirstMessage)
            .ToString();

        return new ReplyPrompt(Fill(templates.System, Values(templates, owner, language, hasPhoto, maxLength)), user, language);
    }

    /// <summary>
    /// The owner did not like the draft and says what to change. Their hint is trusted; the draft goes in tags as text
    /// to rework, and the profile stays data as before.
    /// </summary>
    public static ReplyPrompt BuildRevision(
        PromptTemplates templates,
        Profile profile,
        OwnerProfileOptions owner,
        ReplyLanguage language,
        bool hasPhoto,
        int maxLength,
        ReplyText draft,
        string hint)
    {
        var values = Values(templates, owner, language, hasPhoto, maxLength);
        values[PromptTemplateParser.Hint] = hint.Trim();

        var system = new StringBuilder(Fill(templates.System, values))
            .AppendLine()
            .AppendLine()
            .Append(Fill(templates.RevisionSystem, values))
            .ToString();

        var user = new StringBuilder(ProfileBlock(profile))
            .AppendLine()
            .AppendLine("<draft>")
            .AppendLine(Sanitize(draft.Value))
            .AppendLine("</draft>")
            .AppendLine()
            .Append(Fill(templates.Revision, values))
            .ToString();

        return new ReplyPrompt(system, user, language);
    }

    public static string LanguageName(ReplyLanguage language) => language switch
    {
        ReplyLanguage.Uzbek => "узбекский (тем же алфавитом, что и анкета)",
        ReplyLanguage.English => "английский",
        _ => "русский",
    };

    /// <summary>Stops the profile text from closing or opening tags in the prompt.</summary>
    public static string Sanitize(string value) => value.Replace('<', '‹').Replace('>', '›');

    private static Dictionary<string, string> Values(
        PromptTemplates templates, OwnerProfileOptions owner, ReplyLanguage language, bool hasPhoto, int maxLength) =>
        new(StringComparer.Ordinal)
        {
            ["owner_name"] = OrUnknown(owner.Name),
            ["owner_about"] = OrUnknown(owner.About),
            ["owner_tone"] = OrUnknown(owner.Tone),
            ["owner_avoid"] = OrUnknown(owner.Avoid),
            ["max_length"] = maxLength.ToString(CultureInfo.InvariantCulture),
            ["language"] = LanguageName(language),
            ["photo_rule"] = hasPhoto ? templates.Photo : templates.NoPhoto,
        };

    /// <summary>One pass, so a value that happens to contain braces (your hint, say) is never filled in turn.</summary>
    private static string Fill(string template, Dictionary<string, string> values) =>
        PromptTemplateParser.PlaceholderPattern().Replace(
            template, match => values.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Value);

    private static string ProfileBlock(Profile profile) =>
        new StringBuilder()
            .AppendLine("<profile>")
            .AppendLine($"Имя: {Sanitize(profile.Name.Value)}")
            .AppendLine(profile.Age.HasValue ? $"Возраст: {profile.Age.GetValueOrThrow()}" : "Возраст: не указан")
            .AppendLine(profile.City.HasValue ? $"Город: {Sanitize(profile.City.GetValueOrThrow().Value)}" : "Город: не указан")
            .AppendLine(profile.Bio.HasValue ? $"О себе: {Sanitize(profile.Bio.GetValueOrThrow().Value)}" : "О себе: пусто")
            .AppendLine(profile.Interests.IsEmpty ? "Интересы: не указаны" : $"Интересы: {Sanitize(profile.Interests.ToString())}")
            .Append(Facts(profile))
            .AppendLine("</profile>")
            .ToString();

    private static string Facts(Profile profile) =>
        string.Concat(profile.Facts.Items.Select(fact => Sanitize(fact) + Environment.NewLine));

    private static string OrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();
}
