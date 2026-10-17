using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Sync;

namespace DateBot.Application.Ports.Persistence;

public interface ISyncCursorRepository
{
    Task<Result<SyncCursor, Error>> GetAsync(CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(SyncCursor cursor, CancellationToken ct);
}
