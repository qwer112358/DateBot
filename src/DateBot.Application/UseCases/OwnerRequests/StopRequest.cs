namespace DateBot.Application.UseCases.OwnerRequests;

public sealed record StopRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
