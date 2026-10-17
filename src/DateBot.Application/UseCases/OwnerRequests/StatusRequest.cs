namespace DateBot.Application.UseCases.OwnerRequests;

public sealed record StatusRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
