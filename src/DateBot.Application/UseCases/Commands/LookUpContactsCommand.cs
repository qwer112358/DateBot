using System.Collections.Immutable;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.UseCases.Commands;

/// <summary>"Find in the app" on a draft no channel reached: look the person up again; the result is the channels found.</summary>
public sealed record LookUpContactsCommand(Guid MatchId) : ICommand<ImmutableArray<MessageChannel>>;
