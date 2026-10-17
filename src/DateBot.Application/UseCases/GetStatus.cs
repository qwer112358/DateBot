using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases;

public sealed record GetStatusQuery : ICommand<StatusReport>;

public sealed record StatusReport(
    IReadOnlyDictionary<InboxStatus, int> Inbox,
    IReadOnlyDictionary<MatchStatus, int> Matches,
    long Cursor,
    Maybe<DateTimeOffset> LastProcessedAt,
    bool Paused,
    ProcessingMode Mode,
    int ActionsLastHour,
    int MaxActionsPerHour);

internal sealed class GetStatusHandler(
    IInboxRepository inbox,
    IMatchRepository matches,
    ISyncCursorRepository cursors,
    IControlStateStore control,
    IRateLimiter limiter,
    IOptions<ProcessingOptions> options)
    : ICommandHandler<GetStatusQuery, StatusReport>
{
    public Task<Result<StatusReport, Error>> HandleAsync(GetStatusQuery query, CancellationToken ct) =>
        inbox.CountByStatusAsync(ct)
            .Bind(inboxCounts => matches.CountByStatusAsync(ct)
            .Bind(matchCounts => cursors.GetAsync(ct)
            .Bind(cursor => control.GetAsync(ct)
            .Bind(state => limiter.UsedInLastHourAsync(ct)
            .Map(used => new StatusReport(
                inboxCounts,
                matchCounts,
                cursor.Position,
                cursor.LastProcessedAt,
                state.Paused,
                options.Value.Mode,
                used,
                options.Value.MaxActionsPerHour))))));
}
