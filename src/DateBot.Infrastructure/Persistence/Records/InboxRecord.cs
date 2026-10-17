using DateBot.Domain.Inbox.Enums;

namespace DateBot.Infrastructure.Persistence.Records;

/// <summary>Table <c>BotMessages</c>: every bot message as it arrived.</summary>
public sealed class InboxRecord
{
    public long Id { get; set; }

    public string RawText { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    public InboxStatus Status { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
