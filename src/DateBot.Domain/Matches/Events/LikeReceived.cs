using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.Events;

public sealed record LikeReceived(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;
