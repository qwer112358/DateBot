using System.Collections.Immutable;
using DateBot.Application.Abstractions;
using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases;

/// <summary>"Find in the app" on a draft no channel reached: look the person up again; the result is the channels found.</summary>
public sealed record LookUpContactsCommand(Guid MatchId) : ICommand<ImmutableArray<MessageChannel>>;
