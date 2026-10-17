using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;
using DateBot.Domain.Sync;

namespace DateBot.Application.Tests;

internal sealed class InMemoryRepositories(InMemoryStore store)
    : IMatchRepository, IInboxRepository, ISyncCursorRepository, IControlStateStore
{
    public Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct) =>
        Ok(store.Matches.TryGetValue(id.Value, out var match) ? Maybe.From(match) : Maybe<Match>.None);

    public Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct) =>
        Ok(store.Matches.Values.TryFirst(m => m.PersonId == personId));

    public Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct)
    {
        store.Matches[match.Id.Value] = match;
        return Done();
    }

    public Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct)
    {
        store.Matches.Remove(match.Id.Value);
        store.Forgotten.Add(match.PersonId.Value);
        return Done();
    }

    public Task<Result<bool, Error>> IsForgottenAsync(TashdateUserId personId, CancellationToken ct) =>
        Ok(store.Forgotten.Contains(personId.Value));

    public Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct) =>
        Ok<IReadOnlyDictionary<MatchStatus, int>>(store.Matches.Values.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<Result<IReadOnlyList<PhotoFileId>, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct) =>
        Ok<IReadOnlyList<PhotoFileId>>([]);

    public Task<Result<Maybe<InboxMessage>, Error>> FindAsync(BotMessageId id, CancellationToken ct) =>
        Ok(store.Inbox.TryGetValue(id.Value, out var message) ? Maybe.From(message) : Maybe<InboxMessage>.None);

    public Task<UnitResult<Error>> AddAsync(InboxMessage message, CancellationToken ct)
    {
        store.Inbox[message.Id.Value] = message;
        return Done();
    }

    public Task<UnitResult<Error>> RemoveAsync(BotMessageId id, CancellationToken ct)
    {
        store.Inbox.Remove(id.Value);
        return Done();
    }

    Task<Result<IReadOnlyDictionary<InboxStatus, int>, Error>> IInboxRepository.CountByStatusAsync(CancellationToken ct) =>
        Ok<IReadOnlyDictionary<InboxStatus, int>>(store.Inbox.Values.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<Result<int, Error>> PurgeSentBeforeAsync(DateTimeOffset before, CancellationToken ct) => Ok(0);

    Task<Result<SyncCursor, Error>> ISyncCursorRepository.GetAsync(CancellationToken ct) =>
        Ok(SyncCursor.Restore(store.Cursor.LastProcessed, store.Cursor.LastProcessedAt));

    public Task<UnitResult<Error>> SaveAsync(SyncCursor cursor, CancellationToken ct)
    {
        store.Cursor = cursor;
        return Done();
    }

    Task<Result<ControlState, Error>> IControlStateStore.GetAsync(CancellationToken ct) => Ok(store.Control);

    public Task<UnitResult<Error>> SaveAsync(ControlState state, CancellationToken ct)
    {
        store.Control = state;
        return Done();
    }

    private static Task<Result<T, Error>> Ok<T>(T value) => Task.FromResult(Result.Success<T, Error>(value));

    private static Task<UnitResult<Error>> Done() => Task.FromResult(UnitResult.Success<Error>());
}
