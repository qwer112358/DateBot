using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record ReplyDrafted(
    MatchId MatchId, MatchKind Kind, Profile Profile, ReplyText Text, DateTimeOffset OccurredAt) : IDomainEvent;
