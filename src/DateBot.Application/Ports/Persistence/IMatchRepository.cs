using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Persistence.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Ports.Persistence;

public interface IMatchRepository
{
    Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct);

    Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct);

    /// <summary>
    /// The same person under another key: a match card (username) for someone first met as a like in the app (app id),
    /// or the other way round. Usernames compare case-insensitively; both empty finds nothing.
    /// </summary>
    Task<Result<Maybe<Match>, Error>> FindByContactAsync(Maybe<ContactUsername> username, Maybe<AppProfileId> appId, CancellationToken ct);

    /// <summary>Open matches whose message draft waits for the owner, oldest first. Read-only: nothing is tracked.</summary>
    Task<Result<IReadOnlyList<Match>, Error>> FindWithDraftAsync(CancellationToken ct);

    Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct);

    /// <summary>Deletes the match and keeps only tombstones of its <see cref="Match.IdentityKeys"/>, so they are never answered twice.</summary>
    Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct);

    /// <summary>True when data about any of these keys was deleted earlier (forget command or retention).</summary>
    Task<Result<bool, Error>> IsForgottenAsync(IReadOnlyCollection<string> personKeys, CancellationToken ct);

    Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct);

    /// <summary>
    /// Deletes matches received before <paramref name="before"/>, leaving tombstones of their identity keys;
    /// returns how many and their photo references.
    /// </summary>
    Task<Result<PurgedMatches, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct);
}
