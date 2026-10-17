using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.UseCases.Queries;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.UseCases.Handlers;

internal sealed class GetDraftHandler(IMatchRepository matches) : ICommandHandler<GetDraftQuery, ReplyText>
{
    public Task<Result<ReplyText, Error>> HandleAsync(GetDraftQuery query, CancellationToken ct) =>
        MatchId.Create(query.MatchId)
            .Bind(id => matches.FindByIdAsync(id, ct).Bind(found => found.ToResult(MatchErrors.NotFound(id))))
            .Bind(match => match.DraftMessage.Bind(draft => draft.Text).ToResult(MatchErrors.NoDraft));
}
