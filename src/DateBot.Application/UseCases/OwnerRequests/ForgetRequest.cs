namespace DateBot.Application.UseCases.OwnerRequests;

public sealed record ForgetRequest(long ChatId, long UserId, Guid MatchId) : OwnerRequest(ChatId, UserId);
