using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record LikeReceived(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;
