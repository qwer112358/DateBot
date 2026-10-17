namespace DateBot.Domain.Common;

public interface IAggregateRoot
{
    /// <summary>Returns the raised events and clears them; called by the unit of work after commit.</summary>
    IReadOnlyList<IDomainEvent> PullDomainEvents();
}
