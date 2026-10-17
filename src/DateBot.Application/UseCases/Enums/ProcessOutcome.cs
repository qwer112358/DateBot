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
}
