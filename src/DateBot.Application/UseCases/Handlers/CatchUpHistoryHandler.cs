using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Errors;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Application.Telegram.Models;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Shared.ValueObjects;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Handlers;

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
        EnsureStartPositionAsync(ct).Bind(() => CatchUpFromAsync(CatchUpReport.Empty, previousPosition: -1, ct));

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

    /// <summary>
    /// Batch after batch until the chat is read. Every processed batch moves the cursor; if one did not,
    /// stop instead of fetching the same messages again (the next run retries).
    /// </summary>
    private Task<Result<CatchUpReport, Error>> CatchUpFromAsync(CatchUpReport report, long previousPosition, CancellationToken ct) =>
        control.GetAsync(ct)
            .Ensure(state => !state.Paused, ServiceErrors.Paused)
            .Bind(_ => cursors.GetAsync(ct))
            .Bind(cursor => cursor.Position == previousPosition
                ? Task.FromResult(Result.Success<CatchUpReport, Error>(report))
                : client.GetHistoryAsync(cursor.Position, options.Value.HistoryBatchSize, ct)
                    .Bind(batch => batch.IsEmpty
                        ? Task.FromResult(Result.Success<CatchUpReport, Error>(report))
                        : ProcessBatchAsync(batch, report, ct).Bind(updated => CatchUpFromAsync(updated, cursor.Position, ct))));

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
