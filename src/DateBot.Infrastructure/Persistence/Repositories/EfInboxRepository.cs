using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Persistence;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Inbox;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Shared.ValueObjects;
using DateBot.Infrastructure.Persistence.Mapping;
using DateBot.Infrastructure.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace DateBot.Infrastructure.Persistence.Repositories;

internal sealed class EfInboxRepository(DateBotDbContext db, ChangeSet changes) : IInboxRepository
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
