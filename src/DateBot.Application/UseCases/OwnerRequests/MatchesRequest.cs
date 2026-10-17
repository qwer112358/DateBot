namespace DateBot.Application.UseCases.OwnerRequests;

/// <summary>/matches: the mutual matches you have not written to yet.</summary>
public sealed record MatchesRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
