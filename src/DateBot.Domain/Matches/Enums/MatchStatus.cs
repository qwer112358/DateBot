namespace DateBot.Domain.Matches.Enums;

public enum MatchStatus
{
    Received = 1,
    LikedBack = 2,
    MessageSent = 3,
    Done = 4,
    Failed = 5,

    /// <summary>The owner pressed "Skip" on a draft or a like card, or the filters declined the person.</summary>
    Skipped = 6,
}
