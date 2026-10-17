using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record MutualMatchReceived(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;
