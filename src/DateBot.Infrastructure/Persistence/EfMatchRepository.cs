using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.EntityFrameworkCore;

namespace DateBot.Infrastructure.Persistence;

internal sealed class EfMatchRepository(DateBotDbContext db, ChangeSet changes, IClock clock) : IMatchRepository
{
    public Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct) =>
        changes.FindMatch(id.Value) is { } tracked
            ? Task.FromResult(Result.Success<Maybe<Match>, Error>(tracked))
            : LoadAsync(db.Matches.Where(m => m.Id == id.Value), ct);

    public Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct) =>
        changes.FindMatchByPerson(personId.Value) is { } tracked
            ? Task.FromResult(Result.Success<Maybe<Match>, Error>(tracked))
            : LoadAsync(db.Matches.Where(m => m.PersonId == personId.Value), ct);

    public Task<Result<Maybe<Match>, Error>> FindByContactAsync(Maybe<ContactUsername> username, Maybe<AppProfileId> appId, CancellationToken ct)
    {
        var name = username.HasValue ? username.GetValueOrThrow().Value : null;
        var app = appId.HasValue ? appId.GetValueOrThrow().Value : (long?)null;
        if (name is null && app is null)
        {
            return Task.FromResult(Result.Success<Maybe<Match>, Error>(Maybe<Match>.None));
        }

        var lower = name?.ToLowerInvariant();
        return changes.FindMatchByContact(name, app) is { } tracked
            ? Task.FromResult(Result.Success<Maybe<Match>, Error>(tracked))
            : LoadAsync(
                db.Matches
                    .Where(m => (lower != null && m.Username != null && m.Username.ToLower() == lower) || (app != null && m.AppId == app))
                    .OrderBy(m => m.ReceivedAt),
                ct);
    }

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

    public Task<Result<bool, Error>> IsForgottenAsync(IReadOnlyCollection<string> personKeys, CancellationToken ct)
    {
        var keys = personKeys.ToArray();
        return Db.TryAsync(() => db.ForgottenPeople.AsNoTracking().AnyAsync(f => keys.Contains(f.PersonId), ct));
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
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var expired = db.Matches.Where(m => m.ReceivedAt < before);
            var people = await expired
                .Select(m => new MatchRecord { PersonId = m.PersonId, Username = m.Username, AppId = m.AppId, PhotoFileId = m.PhotoFileId })
                .ToListAsync(ct);
            var known = await db.ForgottenPeople.Select(f => f.PersonId).ToListAsync(ct);
            db.ForgottenPeople.AddRange(people
                .SelectMany(RecordMapper.IdentityKeys)
                .Distinct(StringComparer.Ordinal)
                .Except(known)
                .Select(id => new ForgottenPersonRecord { PersonId = id, ForgottenAt = clock.UtcNow }));
            await db.SaveChangesAsync(ct);
            await expired.ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            return (IReadOnlyList<PhotoFileId>)[.. people
                .Where(p => p.PhotoFileId != null)
                .Select(p => PhotoFileId.Create(p.PhotoFileId!))
                .Where(p => p.IsSuccess)
                .Select(p => p.Value)];
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
