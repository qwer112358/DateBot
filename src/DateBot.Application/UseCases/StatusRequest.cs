namespace DateBot.Application.UseCases;

public sealed record StatusRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
