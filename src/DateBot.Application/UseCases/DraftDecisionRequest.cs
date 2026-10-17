using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases;

/// <param name="Channel">The channel button you pressed; none for the plain "Send" of older cards.</param>
public sealed record DraftDecisionRequest(long ChatId, long UserId, Guid MatchId, bool Approve, MessageChannel? Channel = null)
    : OwnerRequest(ChatId, UserId);
