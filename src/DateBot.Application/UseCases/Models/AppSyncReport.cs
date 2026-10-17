using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.Models;

/// <param name="Open">Likes with a visible profile in the app's list.</param>
/// <param name="Hidden">Likes the app hides until premium; they cannot be answered.</param>
/// <param name="Done">Liked back (and the message sent or not needed yet).</param>
/// <param name="Drafted">Liked back, the draft waits for your choice of channel.</param>
/// <param name="Ignored">Already known, declined by the filters or forgotten.</param>
public sealed record AppSyncReport(int Open, int Hidden, int Done, int Drafted, int Ignored, int Failed)
{
    public int Answered => Done + Drafted;

    public static AppSyncReport Start(int open, int hidden) => new(open, hidden, 0, 0, 0, 0);

    public AppSyncReport Add(ProcessOutcome outcome) => outcome switch
    {
        ProcessOutcome.Done => this with { Done = Done + 1 },
        ProcessOutcome.AwaitingConfirmation => this with { Drafted = Drafted + 1 },
        ProcessOutcome.Failed or ProcessOutcome.ParseFailed => this with { Failed = Failed + 1 },
        _ => this with { Ignored = Ignored + 1 },
    };
}
