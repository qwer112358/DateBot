using System.Globalization;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Shared;
using DateBot.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace DateBot.Infrastructure.Persistence;

/// <summary>
/// Cursor and control flags live in the key/value table <c>SyncState</c>. Reads always go to the database: other
/// scopes (each processed message, the notifier bot) move the cursor and flip /stop while this scope is alive.
/// </summary>
internal sealed class EfSyncStateStore(DateBotDbContext db, ChangeSet changes) : ISyncCursorRepository, IControlStateStore
{
    Task<Result<SyncCursor, Error>> ISyncCursorRepository.GetAsync(CancellationToken ct) =>
        Db.TryAsync(async () =>
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
        Db.TryAsync(async () =>
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
