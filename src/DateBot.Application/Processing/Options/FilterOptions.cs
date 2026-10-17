namespace DateBot.Application.Processing.Options;

/// <summary>Placeholder for profile filters. Disabled by default: everyone gets an answer.</summary>
public sealed class FilterOptions
{
    public bool Enabled { get; init; }

    public int MinAge { get; init; } = 18;

    public int MaxAge { get; init; } = 99;

    /// <summary>Empty = any city.</summary>
    public string[] Cities { get; init; } = [];

    /// <summary>Profiles whose text contains any of these words are skipped.</summary>
    public string[] StopWords { get; init; } = [];
}
