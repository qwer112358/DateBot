using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record ProcessingFailed(MatchId MatchId, Profile Profile, Error Reason, DateTimeOffset OccurredAt) : IDomainEvent;
