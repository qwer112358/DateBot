namespace DateBot.Infrastructure.Persistence;

/// <summary>Table <c>SyncState</c>: cursor and service flags as key/value.</summary>
public sealed class SyncStateRecord
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
