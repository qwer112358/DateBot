using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.Ports;

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
