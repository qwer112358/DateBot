using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Ports.Persistence;

/// <summary>Saves everything changed in the current scope in one transaction, then publishes domain events.</summary>
public interface IUnitOfWork
{
    Task<UnitResult<Error>> CommitAsync(CancellationToken ct);
}
