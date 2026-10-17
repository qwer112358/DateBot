using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>A draft that is gone by now (sent, skipped, forgotten) needs no card.</summary>
internal sealed class OfferDraftHandler(IMatchRepository matches, GenerateAndSendReply reply, IOwnerNotifier notifier)
    : ICommandHandler<OfferDraftCommand, Unit>
{
    public Task<Result<Unit, Error>> HandleAsync(OfferDraftCommand command, CancellationToken ct) =>
        MatchId.Create(command.MatchId)
            .Bind(id => matches.FindByIdAsync(id, ct).Bind(found => found.ToResult(MatchErrors.NotFound(id))))
            .Bind(match => reply.DraftCard(match).HasValue
                ? notifier.NotifyAsync(reply.DraftCard(match).GetValueOrThrow(), ct)
                : Task.FromResult(UnitResult.Success<Error>()))
            .Map(() => Unit.Value);
}
