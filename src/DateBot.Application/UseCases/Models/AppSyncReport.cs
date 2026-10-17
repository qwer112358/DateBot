using DateBot.Application.MiniApp.Options;
using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.Models;

/// <param name="Open">Likes with a visible profile in the app's list.</param>
/// <param name="Hidden">Likes the app hides until premium; they cannot be answered.</param>
/// <param name="Done">Liked back (and the message sent or not needed yet).</param>
/// <param name="Drafted">Liked back, the draft waits for your choice of channel.</param>
/// <param name="Ignored">Already known, declined by the filters or forgotten.</param>
/// <param name="Proposed">New likes sent to you as cards in this pass.</param>
/// <param name="Waiting">Cards sent earlier that still wait for your answer.</param>
/// <param name="Deferred">Likes this pass did not reach because of <see cref="MiniAppOptions.MaxLikesPerPass"/>.</param>
public sealed record AppSyncReport(
    int Open, int Hidden, int Done, int Drafted, int Ignored, int Failed, int Proposed = 0, int Waiting = 0, int Deferred = 0)
{
    public int Answered => Done + Drafted;

    /// <summary>New people this pass took on: they count against <see cref="MiniAppOptions.MaxLikesPerPass"/>.</summary>
    public int Handled => Answered + Proposed;

    public static AppSyncReport Start(int open, int hidden) => new(open, hidden, 0, 0, 0, 0);

    public AppSyncReport Add(ProcessOutcome outcome) => outcome switch
    {
        ProcessOutcome.Done => this with { Done = Done + 1 },
        ProcessOutcome.AwaitingConfirmation => this with { Drafted = Drafted + 1 },
        ProcessOutcome.LikeBackProposed => this with { Proposed = Proposed + 1 },
        ProcessOutcome.LikeBackAwaiting => this with { Waiting = Waiting + 1 },
        ProcessOutcome.Failed or ProcessOutcome.ParseFailed => this with { Failed = Failed + 1 },
        _ => this with { Ignored = Ignored + 1 },
    };
}
