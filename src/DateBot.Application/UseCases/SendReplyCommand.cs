using DateBot.Application.Abstractions;
using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases;

/// <summary>Your decision on a Confirm-mode draft: send it (through <paramref name="Channel"/>, or the preferred one) or skip it.</summary>
public sealed record SendReplyCommand(Guid MatchId, bool Approve, MessageChannel? Channel = null) : ICommand<DraftDecision>;
