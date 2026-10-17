namespace DateBot.Domain.Matches.Enums;

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
