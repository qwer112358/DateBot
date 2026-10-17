namespace DateBot.Application.UseCases.OwnerRequests;

public sealed record LookUpContactsRequest(long ChatId, long UserId, Guid MatchId) : OwnerRequest(ChatId, UserId);
