using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Sync;

namespace DateBot.Application.Ports;

public interface ISyncCursorRepository
{
    Task<Result<SyncCursor, Error>> GetAsync(CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(SyncCursor cursor, CancellationToken ct);
}
