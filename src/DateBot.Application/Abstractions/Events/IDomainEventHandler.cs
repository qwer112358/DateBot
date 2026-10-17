using DateBot.Domain.Common.Primitives;

namespace DateBot.Application.Abstractions.Events;

/// <summary>Reacts to domain events after the aggregate was saved. Handles its own errors.</summary>
public interface IDomainEventHandler
{
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct);
}
