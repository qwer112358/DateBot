using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Enums;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.UseCases.Commands;

/// <summary>Your decision on a Confirm-mode draft: send it (through <paramref name="Channel"/>, or the preferred one) or skip it.</summary>
public sealed record SendReplyCommand(Guid MatchId, bool Approve, MessageChannel? Channel = null) : ICommand<DraftDecision>;
