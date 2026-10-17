namespace DateBot.Application.UseCases;

public sealed record DraftDecisionRequest(long ChatId, long UserId, Guid MatchId, bool Approve) : OwnerRequest(ChatId, UserId);
