namespace DateBot.Application.UseCases;

public sealed record CatchUpReport(int Processed, int Done, int Drafted, int Ignored, int ParseFailed, int Failed)
{
    public static CatchUpReport Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public CatchUpReport Add(ProcessOutcome outcome) => outcome switch
    {
        ProcessOutcome.Done => this with { Processed = Processed + 1, Done = Done + 1 },
        ProcessOutcome.AwaitingConfirmation => this with { Processed = Processed + 1, Drafted = Drafted + 1 },
        ProcessOutcome.Ignored => this with { Processed = Processed + 1, Ignored = Ignored + 1 },
        ProcessOutcome.ParseFailed => this with { Processed = Processed + 1, ParseFailed = ParseFailed + 1 },
        ProcessOutcome.Failed => this with { Processed = Processed + 1, Failed = Failed + 1 },
        _ => this,
    };
}
