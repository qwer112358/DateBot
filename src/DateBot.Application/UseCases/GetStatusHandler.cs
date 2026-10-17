using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

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
