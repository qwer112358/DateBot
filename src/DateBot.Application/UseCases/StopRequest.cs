namespace DateBot.Application.UseCases;

public sealed record StopRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
