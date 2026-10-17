using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Domain.Common;

namespace DateBot.Application.Tests;

internal sealed class FakeUnitOfWork(InMemoryStore store, IDomainEventDispatcher events) : IUnitOfWork
{
    public async Task<UnitResult<Error>> CommitAsync(CancellationToken ct)
    {
        store.Commits++;
        await events.DispatchAsync([.. store.Matches.Values.SelectMany(m => m.PullDomainEvents())], ct);
        return UnitResult.Success<Error>();
    }
}
