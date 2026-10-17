using DateBot.Application.Abstractions.Messaging;

namespace DateBot.Application.UseCases.Commands;

/// <summary>Your answer on a like card: yes starts the like back, no closes the person without a like.</summary>
public sealed record DecideLikeBackCommand(Guid MatchId, bool Approve) : ICommand<Unit>;
