namespace DateBot.Domain.Matches;

/// <summary>How the person reached us.</summary>
public enum MatchKind
{
    /// <summary>The person liked us; scenario A: like back and message.</summary>
    LikedMe = 1,

    /// <summary>Mutual like after our like; scenario B: message only.</summary>
    Mutual = 2,
}

public enum MatchStatus
{
    Received = 1,
    LikedBack = 2,
    MessageSent = 3,
    Done = 4,
    Failed = 5,

    /// <summary>The owner pressed "Skip" on a draft in Confirm mode.</summary>
    Skipped = 6,
}

public enum ActionType
{
    LikeBack = 1,
    SendMessage = 2,
}

public enum ActionStatus
{
    /// <summary>Message prepared in Confirm mode, waiting for the owner.</summary>
    Draft = 1,

    /// <summary>Started but not confirmed yet; survives restarts.</summary>
    Pending = 2,
    Done = 3,
    Failed = 4,
    Skipped = 5,
}
