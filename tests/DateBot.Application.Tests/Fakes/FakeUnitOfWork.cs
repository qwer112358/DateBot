using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Events;
using DateBot.Application.Ports.Persistence;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Tests.Fakes;

internal sealed class FakeUnitOfWork(InMemoryStore store, IDomainEventDispatcher events) : IUnitOfWork
{
    public async Task<UnitResult<Error>> CommitAsync(CancellationToken ct)
    {
        store.Commits++;
        await events.DispatchAsync([.. store.Matches.Values.SelectMany(m => m.PullDomainEvents())], ct);
        return UnitResult.Success<Error>();
    }
}
