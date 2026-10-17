using DateBot.Domain.Common;

namespace DateBot.Application.Abstractions;

/// <summary>Reacts to domain events after the aggregate was saved. Handles its own errors.</summary>
public interface IDomainEventHandler
{
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}

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
