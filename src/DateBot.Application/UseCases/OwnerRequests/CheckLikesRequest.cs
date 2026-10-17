namespace DateBot.Application.UseCases.OwnerRequests;

/// <summary>/likes: check the app's likes now.</summary>
public sealed record CheckLikesRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
