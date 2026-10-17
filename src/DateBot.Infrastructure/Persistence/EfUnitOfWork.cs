using System.Globalization;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Domain.Common;

namespace DateBot.Infrastructure.Persistence;

/// <summary>
/// Writes every aggregate of the scope with one <c>SaveChanges</c> (one SQLite transaction),
/// then publishes the collected domain events.
/// </summary>
internal sealed class EfUnitOfWork(DateBotDbContext db, ChangeSet changes, IDomainEventDispatcher events, IClock clock) : IUnitOfWork
{
    public async Task<UnitResult<Error>> CommitAsync(CancellationToken ct)
    {
        var saved = await Db.TryAsync(async () =>
        {
            await WriteMatchesAsync(ct);
            WriteInbox();
            await RemoveInboxAsync(ct);
            await WriteSyncStateAsync(ct);
            await db.SaveChangesAsync(ct);
            changes.MarkCommitted();
        });

        return await saved.Bind(async () =>
        {
            var domainEvents = changes.Aggregates.SelectMany(a => a.PullDomainEvents()).ToList();
            await events.DispatchAsync(domainEvents, ct);
            return UnitResult.Success<Error>();
        });
    }

    private async Task WriteMatchesAsync(CancellationToken ct)
    {
        foreach (var entry in changes.Matches)
        {
            if (entry.Record is null)
            {
                entry.Record = RecordMapper.ToRecord(entry.Match);
                db.Matches.Add(entry.Record);
            }
            else
            {
                RecordMapper.Apply(entry.Match, entry.Record);
            }
        }

        foreach (var key in changes.ForgottenKeys)
        {
            if (await db.ForgottenPeople.FindAsync([key], ct) is null)
            {
                db.ForgottenPeople.Add(new ForgottenPersonRecord { PersonId = key, ForgottenAt = clock.UtcNow });
            }
        }

        db.Matches.RemoveRange(changes.RemovedMatches);
    }

    private void WriteInbox()
    {
        foreach (var entry in changes.Inbox)
        {
            if (entry.Record is null)
            {
                entry.Record = RecordMapper.ToRecord(entry.Message);
                db.Inbox.Add(entry.Record);
            }
            else
            {
                RecordMapper.Apply(entry.Message, entry.Record);
            }
        }

    }

    private async Task RemoveInboxAsync(CancellationToken ct)
    {
        foreach (var id in changes.RemovedInbox)
        {
            if (await db.Inbox.FindAsync([id], ct) is { } row)
            {
                db.Inbox.Remove(row);
            }
        }
    }

    private async Task WriteSyncStateAsync(CancellationToken ct)
    {
        if (changes.Cursor is { } cursor)
        {
            await UpsertAsync(SyncStateKeys.LastBotMessageId, cursor.Position.ToString(CultureInfo.InvariantCulture), ct);
            await UpsertAsync(
                SyncStateKeys.LastProcessedAt,
                cursor.LastProcessedAt.HasValue ? cursor.LastProcessedAt.GetValueOrThrow().ToString("O", CultureInfo.InvariantCulture) : string.Empty,
                ct);
        }

        if (changes.Control is { } control)
        {
            await UpsertAsync(SyncStateKeys.Paused, control.Paused ? "true" : "false", ct);
            await UpsertAsync(SyncStateKeys.OwnerUserId, Format(control.OwnerUserId), ct);
            await UpsertAsync(SyncStateKeys.OwnerChatId, Format(control.OwnerChatId), ct);
        }
    }

    private async Task UpsertAsync(string key, string value, CancellationToken ct)
    {
        var row = await db.SyncState.FindAsync([key], ct);
        if (row is null)
        {
            db.SyncState.Add(new SyncStateRecord { Key = key, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }

    private static string Format(Maybe<long> value) =>
        value.HasValue ? value.GetValueOrThrow().ToString(CultureInfo.InvariantCulture) : string.Empty;
}
