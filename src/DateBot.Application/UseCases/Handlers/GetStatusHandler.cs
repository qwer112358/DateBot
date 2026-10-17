using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.Queries;
using DateBot.Domain.Common.Errors;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Handlers;

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
