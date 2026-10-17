using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.Ports;

public interface IMatchRepository
{
    Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct);

    Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct);

    /// <summary>
    /// The same person under another key: a match card (username) for someone first met as a like in the app (app id),
    /// or the other way round. Usernames compare case-insensitively; both empty finds nothing.
    /// </summary>
    Task<Result<Maybe<Match>, Error>> FindByContactAsync(Maybe<ContactUsername> username, Maybe<AppProfileId> appId, CancellationToken ct);

    Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct);

    /// <summary>Deletes the match and keeps only tombstones of its <see cref="Match.IdentityKeys"/>, so they are never answered twice.</summary>
    Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct);

    /// <summary>True when data about any of these keys was deleted earlier (forget command or retention).</summary>
    Task<Result<bool, Error>> IsForgottenAsync(IReadOnlyCollection<string> personKeys, CancellationToken ct);

    Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct);

    /// <summary>
    /// Deletes matches received before <paramref name="before"/>, leaving tombstones of their identity keys;
    /// returns their photo references.
    /// </summary>
    Task<Result<IReadOnlyList<PhotoFileId>, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct);
}
