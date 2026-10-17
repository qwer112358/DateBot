using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record LikeReceived(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record MutualMatchReceived(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record LikeBackSent(MatchId MatchId, Profile Profile, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record MessageSent(
    MatchId MatchId, MatchKind Kind, Profile Profile, ReplyText Text, bool LikedBack, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ReplyDrafted(
    MatchId MatchId, MatchKind Kind, Profile Profile, ReplyText Text, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ProcessingFailed(MatchId MatchId, Profile Profile, Error Reason, DateTimeOffset OccurredAt) : IDomainEvent;
