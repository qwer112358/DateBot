using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.EntityFrameworkCore;

namespace DateBot.Infrastructure.Persistence;

/// <summary>Hourly limit computed from the Actions journal, so it survives restarts.</summary>
internal sealed class DbRateLimiter(DateBotDbContext db, IClock clock, Microsoft.Extensions.Options.IOptions<ProcessingOptions> options)
    : IRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public Task<UnitResult<Error>> CheckAsync(CancellationToken ct) =>
        Db.TryAsync(async () =>
        {
            var since = clock.UtcNow - Window;
            var times = await RecentActions(since).Select(a => a.UpdatedAt).ToListAsync(ct);
            if (times.Count < options.Value.MaxActionsPerHour)
            {
                return Result.Success<bool, Error>(true);
            }

            var retryAfter = times.Min() + Window - clock.UtcNow;
            return Result.Failure<bool, Error>(Error.RateLimited(
                "service.hourly_limit",
                $"Достигнут лимит {options.Value.MaxActionsPerHour} действий в час",
                retryAfter < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : retryAfter));
        }).Bind(_ => UnitResult.Success<Error>());

    public Task<Result<int, Error>> UsedInLastHourAsync(CancellationToken ct) =>
        Db.TryAsync(() => RecentActions(clock.UtcNow - Window).CountAsync(ct));

    private IQueryable<ActionRecord> RecentActions(DateTimeOffset since) =>
        db.Actions.AsNoTracking().Where(a =>
            a.UpdatedAt >= since && (a.Status == ActionStatus.Pending || a.Status == ActionStatus.Done));
}
