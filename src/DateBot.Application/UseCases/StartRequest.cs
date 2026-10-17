namespace DateBot.Application.UseCases;

public sealed record StartRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
