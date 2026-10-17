using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Ports.Services;

/// <summary>Hourly limit of outgoing actions (likes and messages).</summary>
public interface IRateLimiter
{
    /// <summary>Fails with a RateLimited error carrying <c>RetryAfter</c> when the limit is reached.</summary>
    Task<UnitResult<Error>> CheckAsync(CancellationToken ct);

    Task<Result<int, Error>> UsedInLastHourAsync(CancellationToken ct);
}
