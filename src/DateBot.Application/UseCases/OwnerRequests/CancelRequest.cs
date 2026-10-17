namespace DateBot.Application.UseCases.OwnerRequests;

/// <summary>/cancel: leave the draft you started editing as it was.</summary>
public sealed record CancelRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
