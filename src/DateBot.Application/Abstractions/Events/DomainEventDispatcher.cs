using DateBot.Domain.Common.Primitives;

namespace DateBot.Application.Abstractions.Events;

internal sealed class DomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            foreach (var handler in handlers)
            {
                await handler.HandleAsync(domainEvent, ct);
            }
        }
    }
}
