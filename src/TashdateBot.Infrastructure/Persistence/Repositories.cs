using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using TashdateBot.Application.Abstractions;
using TashdateBot.Application.Ports;
using TashdateBot.Application.Processing;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Inbox;
using TashdateBot.Domain.Matches;
using TashdateBot.Domain.Shared;
using TashdateBot.Domain.Sync;

namespace TashdateBot.Infrastructure.Persistence;

internal sealed class EfMatchRepository(TashdateDbContext db, ChangeSet changes) : IMatchRepository
{
    public Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct) =>
        changes.FindMatch(id.Value) is { } tracked
            ? Task.FromResult(Result.Success<Maybe<Match>, Error>(tracked))
            : LoadAsync(db.Matches.Where(m => m.Id == id.Value), ct);

    public Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct) =>
        changes.FindMatchByPerson(personId.Value) is { } tracked
            ? Task.FromResult(Result.Success<Maybe<Match>, Error>(tracked))
            : LoadAsync(db.Matches.Where(m => m.PersonId == personId.Value), ct);

    public Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct)
    {
        changes.Track(match, record: null);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct)
    {
        changes.Remove(match);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct) =>
        Db.TryAsync(async () => (IReadOnlyDictionary<MatchStatus, int>)await db.Matches
            .AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct));

    public Task<Result<IReadOnlyList<PhotoFileId>, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct) =>
        Db.TryAsync(async () =>
        {
            var expired = db.Matches.Where(m => m.ReceivedAt < before);
            var photos = await expired.Where(m => m.PhotoFileId != null).Select(m => m.PhotoFileId!).ToListAsync(ct);
            await expired.ExecuteDeleteAsync(ct);
            return (IReadOnlyList<PhotoFileId>)[.. photos.Select(PhotoFileId.Create).Where(p => p.IsSuccess).Select(p => p.Value)];
        });

    private Task<Result<Maybe<Match>, Error>> LoadAsync(IQueryable<MatchRecord> query, CancellationToken ct) =>
        Db.TryAsync(async () =>
        {
            var record = await query.Include(m => m.Actions).FirstOrDefaultAsync(ct);
            return record is null
                ? Result.Success<Maybe<Match>, Error>(Maybe<Match>.None)
                : RecordMapper.ToDomain(record)
                    .Tap(match => changes.Track(match, record))
                    .Map(match => Maybe.From(match));
        });
}

internal sealed class EfInboxRepository(TashdateDbContext db, ChangeSet changes) : IInboxRepository
{
    public Task<Result<Maybe<InboxMessage>, Error>> FindAsync(BotMessageId id, CancellationToken ct) =>
        changes.FindInbox(id.Value) is { } tracked
            ? Task.FromResult(Result.Success<Maybe<InboxMessage>, Error>(tracked))
            : Db.TryAsync(async () =>
            {
                var record = await db.Inbox.FirstOrDefaultAsync(m => m.Id == id.Value, ct);
                return record is null
                    ? Result.Success<Maybe<InboxMessage>, Error>(Maybe<InboxMessage>.None)
                    : RecordMapper.ToDomain(record)
                        .Tap(message => changes.Track(message, record))
                        .Map(message => Maybe.From(message));
            });

    public Task<UnitResult<Error>> AddAsync(InboxMessage message, CancellationToken ct)
    {
        changes.Track(message, record: null);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> RemoveAsync(BotMessageId id, CancellationToken ct)
    {
        changes.RemoveInbox(id.Value);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<Result<IReadOnlyDictionary<InboxStatus, int>, Error>> CountByStatusAsync(CancellationToken ct) =>
        Db.TryAsync(async () => (IReadOnlyDictionary<InboxStatus, int>)await db.Inbox
            .AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct));

    public Task<Result<int, Error>> PurgeSentBeforeAsync(DateTimeOffset before, CancellationToken ct) =>
        Db.TryAsync(() => db.Inbox.Where(m => m.SentAt < before).ExecuteDeleteAsync(ct));
}

/// <summary>Cursor and control flags live in the key/value table <c>SyncState</c>; reads never use tracking.</summary>
internal sealed class EfSyncStateStore(TashdateDbContext db, ChangeSet changes) : ISyncCursorRepository, IControlStateStore
{
    Task<Result<SyncCursor, Error>> ISyncCursorRepository.GetAsync(CancellationToken ct) =>
        changes.Cursor is { } tracked
            ? Task.FromResult(Result.Success<SyncCursor, Error>(tracked))
            : Db.TryAsync(async () =>
            {
                var values = await ReadAsync(ct);
                return ParseLong(values, SyncStateKeys.LastBotMessageId)
                    .Bind(position => position.HasValue
                        ? BotMessageId.Create(position.GetValueOrThrow()).Map(id => Maybe.From(id))
                        : Result.Success<Maybe<BotMessageId>, Error>(Maybe<BotMessageId>.None))
                    .Map<Maybe<BotMessageId>, SyncCursor, Error>(position => SyncCursor.Restore(position, ParseDate(values, SyncStateKeys.LastProcessedAt)));
            });

    public Task<UnitResult<Error>> SaveAsync(SyncCursor cursor, CancellationToken ct)
    {
        changes.Track(cursor);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    Task<Result<ControlState, Error>> IControlStateStore.GetAsync(CancellationToken ct) =>
        changes.Control is { } tracked
            ? Task.FromResult(Result.Success<ControlState, Error>(tracked))
            : Db.TryAsync(async () =>
            {
                var values = await ReadAsync(ct);
                return ParseLong(values, SyncStateKeys.OwnerUserId)
                    .Bind(user => ParseLong(values, SyncStateKeys.OwnerChatId)
                        .Map<Maybe<long>, ControlState, Error>(chat => new ControlState(
                            values.TryGetValue(SyncStateKeys.Paused, out var paused) && paused == "true", user, chat)));
            });

    public Task<UnitResult<Error>> SaveAsync(ControlState state, CancellationToken ct)
    {
        changes.Track(state);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    private Task<Dictionary<string, string>> ReadAsync(CancellationToken ct) =>
        db.SyncState.AsNoTracking().ToDictionaryAsync(r => r.Key, r => r.Value, ct);

    private static Result<Maybe<long>, Error> ParseLong(Dictionary<string, string> values, string key) =>
        !values.TryGetValue(key, out var raw) || raw.Length == 0 ? Maybe<long>.None
        : long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Maybe.From(value)
        : Result.Failure<Maybe<long>, Error>(ServiceErrors.CorruptedData($"SyncState/{key}: «{raw}» не число"));

    private static Maybe<DateTimeOffset> ParseDate(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var raw)
        && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : Maybe<DateTimeOffset>.None;
}

/// <summary>Hourly limit computed from the Actions journal, so it survives restarts.</summary>
internal sealed class DbRateLimiter(TashdateDbContext db, IClock clock, Microsoft.Extensions.Options.IOptions<ProcessingOptions> options)
    : IRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public Task<UnitResult<Error>> CheckAsync(CancellationToken ct) =>
        Db.TryAsync(async () =>
        {
            var since = clock.UtcNow - Window;
            var times = await RecentActions(since).Select(a => a.UpdatedAt).ToListAsync(ct);
            if (times.Count < options.Value.MaxActionsPerHour)
            {
                return Result.Success<bool, Error>(true);
            }

            var retryAfter = times.Min() + Window - clock.UtcNow;
            return Result.Failure<bool, Error>(Error.RateLimited(
                "service.hourly_limit",
                $"Достигнут лимит {options.Value.MaxActionsPerHour} действий в час",
                retryAfter < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : retryAfter));
        }).Bind(_ => UnitResult.Success<Error>());

    public Task<Result<int, Error>> UsedInLastHourAsync(CancellationToken ct) =>
        Db.TryAsync(() => RecentActions(clock.UtcNow - Window).CountAsync(ct));

    private IQueryable<ActionRecord> RecentActions(DateTimeOffset since) =>
        db.Actions.AsNoTracking().Where(a =>
            a.UpdatedAt >= since && (a.Status == ActionStatus.Pending || a.Status == ActionStatus.Done));
}
