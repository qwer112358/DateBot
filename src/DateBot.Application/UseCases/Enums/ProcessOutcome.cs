namespace DateBot.Application.UseCases.Enums;

public enum ProcessOutcome
{
    AlreadyProcessed,
    Done,
    AwaitingConfirmation,
    Ignored,
    ParseFailed,
    Failed,

    /// <summary>The bot hinted at a new like; the likes themselves are read from the app.</summary>
    LikeHint,

    /// <summary>A new like went to the owner as a card; the like back waits for their answer.</summary>
    LikeBackProposed,

    /// <summary>The card was sent earlier and is still unanswered.</summary>
    LikeBackAwaiting,
}
