using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Domain.Common;

namespace DateBot.Application.Tests;

internal sealed class FakeRateLimiter : IRateLimiter
{
    public Queue<Error> Failures { get; } = new();

    public Task<UnitResult<Error>> CheckAsync(CancellationToken ct) =>
        Task.FromResult(Failures.TryDequeue(out var error) ? UnitResult.Failure(error) : UnitResult.Success<Error>());

    public Task<Result<int, Error>> UsedInLastHourAsync(CancellationToken ct) => Task.FromResult(Result.Success<int, Error>(0));
}
