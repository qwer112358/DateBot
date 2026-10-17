using DateBot.Domain.Matches;

namespace DateBot.Infrastructure.Persistence;

/// <summary>Table <c>Profiles</c>: the profile and the state of the match with that person.</summary>
public sealed class MatchRecord
{
    public Guid Id { get; set; }

    public string PersonId { get; set; } = string.Empty;

    public long BotMessageId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Age { get; set; }

    public string? City { get; set; }

    public string? Bio { get; set; }

    /// <summary>One interest per line.</summary>
    public string Interests { get; set; } = string.Empty;

    public string? PhotoFileId { get; set; }

    public string? Username { get; set; }

    public string RawText { get; set; } = string.Empty;

    public MatchKind Kind { get; set; }

    public MatchStatus Status { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<ActionRecord> Actions { get; set; } = [];
}
