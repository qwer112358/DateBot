using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record LikeBackSent(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;
