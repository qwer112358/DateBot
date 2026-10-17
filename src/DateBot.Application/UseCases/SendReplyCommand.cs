using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

/// <summary>Your decision on a Confirm-mode draft: send it or skip it.</summary>
public sealed record SendReplyCommand(Guid MatchId, bool Approve) : ICommand<DraftDecision>;
