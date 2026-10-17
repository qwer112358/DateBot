namespace DateBot.Application.UseCases;

public enum ProcessOutcome
{
    AlreadyProcessed,
    Done,
    AwaitingConfirmation,
    Ignored,
    ParseFailed,
    Failed,
}
