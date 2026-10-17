namespace DateBot.Infrastructure.Llm.Options;

/// <summary>The API connection only; which model and how it writes is llm.json (<c>LlmSettings</c>).</summary>
public sealed class AnthropicOptions
{
    public const string Section = "Anthropic";

    /// <summary>Empty = no LLM, templates only.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
}
