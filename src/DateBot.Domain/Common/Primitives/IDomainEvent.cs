namespace DateBot.Domain.Common.Primitives;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
