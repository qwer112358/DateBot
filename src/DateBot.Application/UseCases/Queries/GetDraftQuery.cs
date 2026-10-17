using DateBot.Application.Abstractions.Messaging;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.UseCases.Queries;

/// <summary>The current text of a match's draft, for you to edit.</summary>
public sealed record GetDraftQuery(Guid MatchId) : ICommand<ReplyText>;
