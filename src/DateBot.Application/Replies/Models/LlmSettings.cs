using DateBot.Application.Replies.Enums;

namespace DateBot.Application.Replies.Models;

/// <summary>
/// How messages are generated, from the llm.json you edit: read before every message, so an edit applies to the next
/// draft without a restart. The defaults are the shipped values.
/// </summary>
public sealed record LlmSettings
{
    public string Model { get; init; } = "claude-haiku-5-5";

    public ModelEffort Effort { get; init; } = ModelEffort.Low;

    /// <summary>Limit of the model's answer in tokens, thinking included.</summary>
    public int MaxTokens { get; init; } = 2048;

    /// <summary>How many profile photos, main first, the model gets; 0 = none. Notifications show them all anyway.</summary>
    public int MaxPhotos { get; init; } = 10;

    /// <summary>
    /// Profile lines the model does not get, by their label before the colon ("Цель знакомства"). Your cards still show them.
    /// </summary>
    public string[] HiddenFacts { get; init; } = ["Цель знакомства"];

    public int MinLength { get; init; } = 20;

    public int MaxLength { get; init; } = 400;

    public int MaxEmoji { get; init; } = 1;

    /// <summary>Used when the profile language cannot be detected.</summary>
    public ReplyLanguage DefaultLanguage { get; init; } = ReplyLanguage.Russian;

    public InvalidReplyPolicy InvalidReplyPolicy { get; init; } = InvalidReplyPolicy.Template;

    /// <summary>Fallback messages per language (keys: Russian, Uzbek, English). <c>{name}</c> is replaced.</summary>
    public Dictionary<string, string[]> Templates { get; init; } = [];
}
