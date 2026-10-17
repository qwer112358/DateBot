using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.Events;

/// <param name="Mutual">The like back produced a match at once (the app answers with it right away).</param>
public sealed record LikeBackSent(MatchId MatchId, Profile Profile, bool Mutual, DateTimeOffset OccurredAt) : IDomainEvent;
