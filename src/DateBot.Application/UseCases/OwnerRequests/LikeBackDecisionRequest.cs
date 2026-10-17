namespace DateBot.Application.UseCases.OwnerRequests;

/// <summary>«❤️ Ответить» or «⏭ Пропустить» on a like card.</summary>
public sealed record LikeBackDecisionRequest(long ChatId, long UserId, Guid MatchId, bool Approve) : OwnerRequest(ChatId, UserId);
