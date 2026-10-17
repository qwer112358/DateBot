namespace DateBot.Application.UseCases.OwnerRequests;

/// <summary>/drafts: send every waiting draft card again.</summary>
public sealed record DraftsRequest(long ChatId, long UserId) : OwnerRequest(ChatId, UserId);
