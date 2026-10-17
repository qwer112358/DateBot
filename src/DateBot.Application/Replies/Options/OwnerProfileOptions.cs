namespace DateBot.Application.Replies.Options;

/// <summary>Short description of you for the prompt.</summary>
public sealed class OwnerProfileOptions
{
    public string Name { get; init; } = string.Empty;

    public string About { get; init; } = string.Empty;

    public string Tone { get; init; } = string.Empty;

    /// <summary>Topics or phrases the message must not contain.</summary>
    public string Avoid { get; init; } = string.Empty;
}
