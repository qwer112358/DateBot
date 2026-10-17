using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.EntityFrameworkCore;

namespace DateBot.Infrastructure.Persistence;

internal sealed class EfMatchRepository(DateBotDbContext db, ChangeSet changes) : IMatchRepository
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
