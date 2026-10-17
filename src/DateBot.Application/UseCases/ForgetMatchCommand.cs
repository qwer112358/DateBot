using DateBot.Application.Abstractions;

namespace DateBot.Application.UseCases;

/// <summary>Deletes everything stored about one person: profile, actions, raw notification, photo.</summary>
public sealed record ForgetMatchCommand(Guid MatchId) : ICommand<Unit>;
