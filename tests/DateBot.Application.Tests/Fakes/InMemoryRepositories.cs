using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Persistence.Models;
using DateBot.Application.Processing.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Inbox;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;
using DateBot.Domain.Sync;

namespace DateBot.Application.Tests.Fakes;

internal sealed class InMemoryRepositories(InMemoryStore store)
    : IMatchRepository, IInboxRepository, ISyncCursorRepository, IControlStateStore
{
    public Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct) =>
        Ok(store.Matches.TryGetValue(id.Value, out var match) ? Maybe.From(match) : Maybe<Match>.None);

    public Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct) =>
        Ok(store.Matches.Values.TryFirst(m => m.PersonId == personId));

    public Task<Result<Maybe<Match>, Error>> FindByContactAsync(Maybe<ContactUsername> username, Maybe<AppProfileId> appId, CancellationToken ct) =>
        Ok(store.Matches.Values.TryFirst(m =>
            (username.HasValue && m.Profile.Username.HasValue
                && string.Equals(m.Profile.Username.GetValueOrThrow().Value, username.GetValueOrThrow().Value, StringComparison.OrdinalIgnoreCase))
            || (appId.HasValue && m.Profile.AppId == appId)));

    public Task<Result<IReadOnlyList<Match>, Error>> FindWithDraftAsync(CancellationToken ct) =>
        Ok<IReadOnlyList<Match>>([.. store.Matches.Values.Where(m => !m.IsClosed && m.DraftMessage.HasValue).OrderBy(m => m.ReceivedAt)]);

    public Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct)
    {
        store.Matches[match.Id.Value] = match;
        return Done();
    }

    public Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct)
    {
        store.Matches.Remove(match.Id.Value);
        store.Forgotten.UnionWith(match.IdentityKeys);
        return Done();
    }

    public Task<Result<bool, Error>> IsForgottenAsync(IReadOnlyCollection<string> personKeys, CancellationToken ct) =>
        Ok(personKeys.Any(store.Forgotten.Contains));

    public Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct) =>
        Ok<IReadOnlyDictionary<MatchStatus, int>>(store.Matches.Values.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<Result<PurgedMatches, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct) =>
        Ok(new PurgedMatches(0, []));

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
