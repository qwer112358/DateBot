namespace DateBot.Domain.Matches;

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
