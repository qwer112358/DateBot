using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Errors;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Domain.Common.Errors;
using Microsoft.Extensions.Options;

namespace DateBot.Application.Processing.Services;

/// <summary>Runs before every outgoing action: honours /stop, the hourly limit and a random pause.</summary>
public sealed class ActionPacer(
    IControlStateStore control,
    IRateLimiter limiter,
    IJitter jitter,
    IClock clock,
    IOptions<ProcessingOptions> options)
{
    public Task<UnitResult<Error>> BeforeActionAsync(CancellationToken ct) =>
        control.GetAsync(ct)
            .Ensure(state => !state.Paused, ServiceErrors.Paused)
            .Bind(_ => limiter.CheckAsync(ct))
            .Tap(() => clock.DelayAsync(jitter.Next(options.Value.MinPause, options.Value.MaxPause), ct));
}
