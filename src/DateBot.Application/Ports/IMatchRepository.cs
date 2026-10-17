using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.Ports;

public interface IMatchRepository
{
    Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct);

    Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct);

    Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct);

    /// <summary>Deletes the match and keeps only a tombstone of the person id, so they are never answered twice.</summary>
    Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct);

    /// <summary>True when the person's data was deleted earlier (forget command or retention).</summary>
    Task<Result<bool, Error>> IsForgottenAsync(TashdateUserId personId, CancellationToken ct);

    Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct);

    /// <summary>
    /// Deletes matches received before <paramref name="before"/>, leaving tombstones of the person ids;
    /// returns their photo references.
    /// </summary>
    Task<Result<IReadOnlyList<PhotoFileId>, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct);
}
