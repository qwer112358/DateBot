using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.Events;

public sealed record MutualMatchReceived(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;
