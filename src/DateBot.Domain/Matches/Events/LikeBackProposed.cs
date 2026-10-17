using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.Events;

/// <summary>A like back waits for the owner's yes or no on a card.</summary>
public sealed record LikeBackProposed(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;
