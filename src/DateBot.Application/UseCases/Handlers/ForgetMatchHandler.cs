using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.UseCases.Handlers;

internal sealed class ForgetMatchHandler(
    IMatchRepository matches,
    IInboxRepository inbox,
    IPhotoStore photos,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ForgetMatchCommand, Unit>
{
    public Task<Result<Unit, Error>> HandleAsync(ForgetMatchCommand command, CancellationToken ct) =>
        MatchId.Create(command.MatchId)
            .Bind(id => matches.FindByIdAsync(id, ct).Bind(found => found.ToResult(MatchErrors.NotFound(id))))
            .Bind(match => DeletePhotoAsync(match, ct)
                .Bind(() => matches.RemoveAsync(match, ct))
                .Bind(() => match.SourceMessageId.HasValue
                    ? inbox.RemoveAsync(match.SourceMessageId.GetValueOrThrow(), ct)
                    : Task.FromResult(UnitResult.Success<Error>()))
                .Bind(() => unitOfWork.CommitAsync(ct)))
            .Map(() => Unit.Value);

    private Task<UnitResult<Error>> DeletePhotoAsync(Match match, CancellationToken ct) =>
        match.Profile.Photo.HasValue
            ? photos.DeleteAsync(match.Profile.Photo.GetValueOrThrow(), ct)
            : Task.FromResult(UnitResult.Success<Error>());
}
