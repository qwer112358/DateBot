namespace DateBot.Application.UseCases;

public sealed record LookUpContactsRequest(long ChatId, long UserId, Guid MatchId) : OwnerRequest(ChatId, UserId);
