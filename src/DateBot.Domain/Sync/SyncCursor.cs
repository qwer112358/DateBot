using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Sync;

/// <summary>Last bot message that reached a final status. Moves only forward.</summary>
public sealed class SyncCursor
{
    private SyncCursor(Maybe<BotMessageId> lastProcessed, Maybe<DateTimeOffset> lastProcessedAt)
    {
        LastProcessed = lastProcessed;
        LastProcessedAt = lastProcessedAt;
    }

    public Maybe<BotMessageId> LastProcessed { get; private set; }

    public Maybe<DateTimeOffset> LastProcessedAt { get; private set; }

    /// <summary>Telegram <c>min_id</c> for history requests: 0 when nothing was processed yet.</summary>
    public long Position => LastProcessed.HasValue ? LastProcessed.GetValueOrThrow().Value : 0;

    public static SyncCursor Start() => new(Maybe<BotMessageId>.None, Maybe<DateTimeOffset>.None);

    public static SyncCursor Restore(Maybe<BotMessageId> lastProcessed, Maybe<DateTimeOffset> lastProcessedAt) =>
        new(lastProcessed, lastProcessedAt);

    public bool IsProcessed(BotMessageId id) => id.Value <= Position;

    public UnitResult<Error> Advance(BotMessageId to, DateTimeOffset now) =>
        Guard.Require(to.Value > Position, SyncCursorErrors.MustMoveForward(Position, to))
            .Tap(() =>
            {
                LastProcessed = to;
                LastProcessedAt = now;
            });
}
