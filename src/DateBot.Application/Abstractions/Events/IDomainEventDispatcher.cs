using DateBot.Domain.Common.Primitives;

namespace DateBot.Application.Abstractions.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}
