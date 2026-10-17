using DateBot.Domain.Inbox;
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

/// <summary>Table <c>SyncState</c>: cursor and service flags as key/value.</summary>
public sealed class SyncStateRecord
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public static class SyncStateKeys
{
    public const string LastBotMessageId = "last_bot_message_id";
    public const string LastProcessedAt = "last_processed_at";
    public const string Paused = "paused";
    public const string OwnerUserId = "owner_user_id";
    public const string OwnerChatId = "owner_chat_id";
}
