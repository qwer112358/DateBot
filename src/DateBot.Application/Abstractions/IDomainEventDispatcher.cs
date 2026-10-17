using DateBot.Domain.Common;

namespace DateBot.Application.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}
