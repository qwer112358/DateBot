namespace DateBot.Application.UseCases.OwnerRequests;

public sealed record UnknownRequest(long ChatId, long UserId, string Text) : OwnerRequest(ChatId, UserId);
