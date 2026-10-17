using CSharpFunctionalExtensions;
using TashdateBot.Application.Processing;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Inbox;
using TashdateBot.Domain.Matches;
using TashdateBot.Domain.Shared;
using TashdateBot.Domain.Sync;

namespace TashdateBot.Application.Ports;

public interface IMatchRepository
{
    Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct);

    Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct);

    Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct);

    Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct);

    Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct);

    /// <summary>Deletes matches received before <paramref name="before"/>; returns their photo references.</summary>
    Task<Result<IReadOnlyList<PhotoFileId>, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct);
}

public interface IInboxRepository
{
    Task<Result<Maybe<InboxMessage>, Error>> FindAsync(BotMessageId id, CancellationToken ct);

    Task<UnitResult<Error>> AddAsync(InboxMessage message, CancellationToken ct);

    Task<UnitResult<Error>> RemoveAsync(BotMessageId id, CancellationToken ct);

    Task<Result<IReadOnlyDictionary<InboxStatus, int>, Error>> CountByStatusAsync(CancellationToken ct);

    Task<Result<int, Error>> PurgeSentBeforeAsync(DateTimeOffset before, CancellationToken ct);
}

public interface ISyncCursorRepository
{
    Task<Result<SyncCursor, Error>> GetAsync(CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(SyncCursor cursor, CancellationToken ct);
}

/// <summary>Paused flag and owner identity, persisted so that /stop survives a restart.</summary>
public interface IControlStateStore
{
    Task<Result<ControlState, Error>> GetAsync(CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(ControlState state, CancellationToken ct);
}

/// <summary>Saves everything changed in the current scope in one transaction, then publishes domain events.</summary>
public interface IUnitOfWork
{
    Task<UnitResult<Error>> CommitAsync(CancellationToken ct);
}
