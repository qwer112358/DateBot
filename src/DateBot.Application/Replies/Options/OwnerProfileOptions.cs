namespace DateBot.Application.Replies.Options;

/// <summary>You, for the prompt; kept in user-secrets. Tone and taboos are written in prompts.md itself.</summary>
public sealed class OwnerProfileOptions
{
    public string Name { get; init; } = string.Empty;

    public string About { get; init; } = string.Empty;
}
