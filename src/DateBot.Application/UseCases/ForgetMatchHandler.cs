using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases;

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
