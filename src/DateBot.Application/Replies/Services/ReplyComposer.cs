using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Models;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Validators;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.Replies.Services;

/// <summary>
/// LLM text when it passes the checks, otherwise a template. The prompts and <see cref="LlmSettings"/> are read for
/// every message, so your edits of prompts.md and llm.json apply to the next one.
/// </summary>
public sealed class ReplyComposer(
    IReplyGenerator generator,
    IPromptTemplates templates,
    ILlmSettings llm,
    IJitter jitter,
    IOptions<ReplyOptions> options,
    ILogger<ReplyComposer> logger)
{
    private static readonly GeneratedReplyValidator Validator = new();

    /// <param name="photos">Every stored photo, main first; at most <see cref="LlmSettings.MaxPhotos"/> reach the model.</param>
    public Task<Result<ComposedReply, Error>> ComposeAsync(Profile profile, IReadOnlyList<ProfilePhoto> photos, CancellationToken ct) =>
        llm.LoadAsync(ct).Bind(settings => ComposeAsync(profile, Cap(photos, settings), settings, ct));

    /// <summary>
    /// Rewrites the draft by the owner's hint. They read the result before it goes anywhere, so a failure is reported
    /// instead of falling back to a template, and the language check follows the text: the hint may ask for another language.
    /// </summary>
    public Task<Result<ReplyText, Error>> ReviseAsync(
        Profile profile, IReadOnlyList<ProfilePhoto> photos, ReplyText draft, string hint, CancellationToken ct) =>
        llm.LoadAsync(ct).Bind(settings =>
        {
            var language = ProfileLanguage(profile).GetValueOrDefault(settings.DefaultLanguage);
            var sent = Cap(photos, settings);
            return templates.LoadAsync(ct)
                .Map(loaded => PromptBuilder.BuildRevision(loaded, profile, options.Value.Owner, language, sent.Count > 0, settings.MaxLength, draft, hint, settings.HiddenFacts))
                .Bind(prompt => generator.GenerateAsync(prompt, sent, settings, ct))
                .Map(Clean)
                .Bind(text => Check(text, LanguageDetector.Detect(text).GetValueOrDefault(language), settings))
                .TapError(error => logger.LogWarning("Rewrite for {Name} rejected: {Error}", profile.Name, error));
        });

    /// <summary>
    /// The bio is the person's own words, so it decides first; interests and city may be labels of the app
    /// in the owner's language and are used only when the bio says nothing.
    /// </summary>
    public static Maybe<ReplyLanguage> ProfileLanguage(Profile profile) =>
        profile.Bio.Bind(bio => LanguageDetector.Detect(bio.Value))
            .Or(() => LanguageDetector.Detect(string.Join(
                " ",
                profile.Interests.ToString(),
                profile.City.Map(c => c.Value).GetValueOrDefault(string.Empty))));

    private async Task<Result<ComposedReply, Error>> ComposeAsync(
        Profile profile, IReadOnlyList<ProfilePhoto> photos, LlmSettings settings, CancellationToken ct)
    {
        var language = ProfileLanguage(profile).GetValueOrDefault(settings.DefaultLanguage);

        if (!profile.HasDetails && photos.Count == 0)
        {
            return Template(profile, language, "анкета пустая", settings);
        }

        var generated = await templates.LoadAsync(ct)
            .Map(loaded => PromptBuilder.Build(loaded, profile, options.Value.Owner, language, photos.Count > 0, settings.MaxLength, settings.HiddenFacts))
            .Bind(prompt => generator.GenerateAsync(prompt, photos, settings, ct));

        return generated
            .Map(Clean)
            .Bind(text => Check(text, language, settings))
            .Map(text => new ComposedReply(text, ReplySource.Llm, Maybe<string>.None))
            .TapError(error => logger.LogWarning("LLM text rejected for {Name}: {Error}", profile.Name, error))
            .OnFailureCompensate(error => Template(profile, language, error.Message, settings));
    }

    private static IReadOnlyList<ProfilePhoto> Cap(IReadOnlyList<ProfilePhoto> photos, LlmSettings settings) =>
        [.. photos.Take(settings.MaxPhotos)];

    private static string Clean(string text) => text.Trim().Trim('"', '«', '»', '“', '”').Trim();

    private static Result<ReplyText, Error> Check(string text, ReplyLanguage language, LlmSettings settings) =>
        Validator
            .Validate(new ReplyCandidate(text, language, settings.MinLength, settings.MaxLength, settings.MaxEmoji))
            .ToResult("reply.rejected")
            .Bind(() => ReplyText.Create(text));

    private Result<ComposedReply, Error> Template(Profile profile, ReplyLanguage language, string reason, LlmSettings settings)
    {
        var templates = settings.Templates.TryGetValue(language.ToString(), out var list) && list.Length > 0
            ? list
            : settings.Templates.Values.SelectMany(t => t).ToArray();
        if (templates.Length == 0)
        {
            return Result.Failure<ComposedReply, Error>(Error.Validation("reply.no_templates", "Не заданы шаблоны сообщений"));
        }

        var text = templates[jitter.NextIndex(templates.Length)].Replace("{name}", profile.Name.Value, StringComparison.Ordinal);
        return ReplyText.Create(text).Map(t => new ComposedReply(
            t, ReplySource.Template, Maybe.From(reason), NeedsReview: settings.InvalidReplyPolicy == InvalidReplyPolicy.Confirm));
    }
}
