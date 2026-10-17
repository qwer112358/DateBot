namespace DateBot.Application.UseCases.OwnerRequests;

public sealed record StartRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
