namespace TashdateBot.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public interface IAggregateRoot
{
    /// <summary>Returns the raised events and clears them; called by the unit of work after commit.</summary>
    IReadOnlyList<IDomainEvent> PullDomainEvents();
}

public abstract class AggregateRoot<TId>(TId id) : IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _events = [];

    public TId Id { get; } = id;

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var events = _events.ToArray();
        _events.Clear();
        return events;
    }
}
