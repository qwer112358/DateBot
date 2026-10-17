using DateBot.Domain.Common;

namespace DateBot.Application.Abstractions;

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
