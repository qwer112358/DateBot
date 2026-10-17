namespace DateBot.Infrastructure.Llm.Options;

public sealed class AnthropicOptions
{
    public const string Section = "Anthropic";

    /// <summary>Empty = no LLM, templates only.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "claude-haiku-5-5";

    public int MaxTokens { get; init; } = 2048;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
}
