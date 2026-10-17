namespace DateBot.Application.UseCases;

public sealed record UnknownRequest(long ChatId, long UserId, string Text) : OwnerRequest(ChatId, UserId);
