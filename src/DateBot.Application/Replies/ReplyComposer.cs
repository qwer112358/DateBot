using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.Replies;

/// <summary>LLM text when it passes the checks, otherwise a template.</summary>
public sealed class ReplyComposer(
    IReplyGenerator generator,
    IJitter jitter,
    IOptions<ReplyOptions> options,
    ILogger<ReplyComposer> logger)
{
    private static readonly GeneratedReplyValidator Validator = new();

    public async Task<Result<ComposedReply, Error>> ComposeAsync(Profile profile, Maybe<ProfilePhoto> photo, CancellationToken ct)
    {
        var settings = options.Value;
        var language = ProfileLanguage(profile).GetValueOrDefault(settings.DefaultLanguage);

        if (!profile.HasDetails && photo.HasNoValue)
        {
            return Template(profile, language, "анкета пустая");
        }

        var prompt = PromptBuilder.Build(profile, settings.Owner, language, photo.HasValue, settings.MaxLength);
        var generated = await generator.GenerateAsync(prompt, photo, ct);

        return generated
            .Map(Clean)
            .Bind(text => Check(text, language, settings))
            .Map(text => new ComposedReply(text, ReplySource.Llm, Maybe<string>.None))
            .TapError(error => logger.LogWarning("LLM text rejected for {Name}: {Error}", profile.Name, error))
            .OnFailureCompensate(error => Template(profile, language, error.Message));
    }

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

    private static string Clean(string text) => text.Trim().Trim('"', '«', '»', '“', '”').Trim();

    private static Result<ReplyText, Error> Check(string text, ReplyLanguage language, ReplyOptions settings) =>
        Validator
            .Validate(new ReplyCandidate(text, language, settings.MinLength, settings.MaxLength, settings.MaxEmoji))
            .ToResult("reply.rejected")
            .Bind(() => ReplyText.Create(text));

    private Result<ComposedReply, Error> Template(Profile profile, ReplyLanguage language, string reason)
    {
        var templates = options.Value.Templates.TryGetValue(language.ToString(), out var list) && list.Length > 0
            ? list
            : options.Value.Templates.Values.SelectMany(t => t).ToArray();
        if (templates.Length == 0)
        {
            return Result.Failure<ComposedReply, Error>(Error.Validation("reply.no_templates", "Не заданы шаблоны сообщений"));
        }

        var text = templates[jitter.NextIndex(templates.Length)].Replace("{name}", profile.Name.Value, StringComparison.Ordinal);
        return ReplyText.Create(text).Map(t => new ComposedReply(t, ReplySource.Template, Maybe.From(reason)));
    }
}
