using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

/// <param name="Mutual">The like back produced a match at once (the app answers with it right away).</param>
public sealed record LikeBackSent(MatchId MatchId, Profile Profile, bool Mutual, DateTimeOffset OccurredAt) : IDomainEvent;
