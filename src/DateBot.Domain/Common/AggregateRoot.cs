namespace DateBot.Domain.Common;

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
