using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using TashdateBot.Application.Abstractions;
using TashdateBot.Application.Ports;
using TashdateBot.Domain.Common;

namespace TashdateBot.Application.Processing;

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
