using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Application.Ports;

/// <summary>Saves everything changed in the current scope in one transaction, then publishes domain events.</summary>
public interface IUnitOfWork
{
    Task<UnitResult<Error>> CommitAsync(CancellationToken ct);
}
