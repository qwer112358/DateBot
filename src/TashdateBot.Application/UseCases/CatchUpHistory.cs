using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using TashdateBot.Application.Abstractions;
using TashdateBot.Application.Ports;
using TashdateBot.Application.Processing;
using TashdateBot.Application.Telegram;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Shared;

namespace TashdateBot.Application.UseCases;

public sealed record CatchUpHistoryCommand : ICommand<CatchUpReport>;

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

/// <summary>
/// Reads the bot chat from the cursor (<c>messages.getHistory</c> with <c>min_id</c>) and processes messages one by
/// one, oldest first, each in its own scope. Used at start-up and whenever the bot writes something new.
/// </summary>
internal sealed class CatchUpHistoryHandler(
    ITashdateClient client,
    ISyncCursorRepository cursors,
    IControlStateStore control,
    IUnitOfWork unitOfWork,
    ICommandDispatcher dispatcher,
    IClock clock,
    IOptions<ProcessingOptions> options)
    : ICommandHandler<CatchUpHistoryCommand, CatchUpReport>
{
    public Task<Result<CatchUpReport, Error>> HandleAsync(CatchUpHistoryCommand command, CancellationToken ct) =>
        EnsureStartPositionAsync(ct).Bind(() => CatchUpFromAsync(CatchUpReport.Empty, ct));

    /// <summary>
    /// The very first run has no cursor: start at "now minus FirstRunBackfill" instead of the beginning of the chat,
    /// so likes from months ago are not answered.
    /// </summary>
    private Task<UnitResult<Error>> EnsureStartPositionAsync(CancellationToken ct) =>
        cursors.GetAsync(ct).Bind(cursor => cursor.LastProcessed.HasValue
            ? Task.FromResult(UnitResult.Success<Error>())
            : client.GetPositionBeforeAsync(clock.UtcNow - options.Value.FirstRunBackfill, ct)
                .Bind(position => position == 0
                    ? Task.FromResult(UnitResult.Success<Error>())
                    : BotMessageId.Create(position)
                        .Bind(start => cursor.Advance(start, clock.UtcNow))
                        .Bind(() => cursors.SaveAsync(cursor, ct))
                        .Bind(() => unitOfWork.CommitAsync(ct))));

    private Task<Result<CatchUpReport, Error>> CatchUpFromAsync(CatchUpReport report, CancellationToken ct) =>
        NextBatchAsync(ct).Bind(batch => batch.IsEmpty
            ? Task.FromResult(Result.Success<CatchUpReport, Error>(report))
            : ProcessBatchAsync(batch, report, ct).Bind(updated => CatchUpFromAsync(updated, ct)));

    private Task<Result<ImmutableArray<RawBotMessage>, Error>> NextBatchAsync(CancellationToken ct) =>
        control.GetAsync(ct)
            .Ensure(state => !state.Paused, ServiceErrors.Paused)
            .Bind(_ => cursors.GetAsync(ct))
            .Bind(cursor => client.GetHistoryAsync(cursor.Position, options.Value.HistoryBatchSize, ct));

    private async Task<Result<CatchUpReport, Error>> ProcessBatchAsync(
        ImmutableArray<RawBotMessage> batch, CatchUpReport report, CancellationToken ct)
    {
        var result = Result.Success<CatchUpReport, Error>(report);
        foreach (var message in batch.OrderBy(m => m.Id))
        {
            // After the first failure Bind short-circuits: later messages wait for the next run.
            result = await result.Bind(current => dispatcher
                .SendAsync<ProcessBotMessageCommand, ProcessOutcome>(new ProcessBotMessageCommand(message), ct)
                .Map(current.Add));
        }

        return result;
    }
}
