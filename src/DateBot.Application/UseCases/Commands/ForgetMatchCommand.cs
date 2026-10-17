using DateBot.Application.Abstractions.Messaging;

namespace DateBot.Application.UseCases.Commands;

/// <summary>Deletes everything stored about one person: profile, actions, raw notification, photo.</summary>
public sealed record ForgetMatchCommand(Guid MatchId) : ICommand<Unit>;
