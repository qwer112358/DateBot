namespace DateBot.Domain.Matches.Enums;

public enum ActionStatus
{
    /// <summary>Waiting for the owner: a message draft (Confirm mode) or a like back offered on a card.</summary>
    Draft = 1,

    /// <summary>Started but not confirmed yet; survives restarts.</summary>
    Pending = 2,
    Done = 3,
    Failed = 4,
    Skipped = 5,
}
