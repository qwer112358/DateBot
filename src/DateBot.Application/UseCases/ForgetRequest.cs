namespace DateBot.Application.UseCases;

public sealed record ForgetRequest(long ChatId, long UserId, Guid MatchId) : OwnerRequest(ChatId, UserId);
