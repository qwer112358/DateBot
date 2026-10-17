namespace DateBot.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
