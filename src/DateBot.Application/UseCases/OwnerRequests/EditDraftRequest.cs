using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.OwnerRequests;

/// <summary>«✏️ Изменить» or «🤖 Переписать» on a draft card: your next message edits that draft.</summary>
public sealed record EditDraftRequest(long ChatId, long UserId, Guid MatchId, DraftEditKind Kind) : OwnerRequest(ChatId, UserId);
