using DateBot.Domain.Matches;

namespace DateBot.Infrastructure.Persistence;

/// <summary>Table <c>Actions</c>: journal of likes and messages and their results.</summary>
public sealed class ActionRecord
{
    public long Id { get; set; }

    public Guid ProfileId { get; set; }

    public int Sequence { get; set; }

    public ActionType Type { get; set; }

    public ActionStatus Status { get; set; }

    public string? Error { get; set; }

    public string? MessageText { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
