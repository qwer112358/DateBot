using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.Events;

public sealed record ProcessingFailed(MatchId MatchId, Profile Profile, Error Reason, DateTimeOffset OccurredAt) : IDomainEvent;
