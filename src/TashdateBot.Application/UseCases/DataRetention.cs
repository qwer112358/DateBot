using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.Extensions.Options;
using TashdateBot.Application.Abstractions;
using TashdateBot.Application.Ports;
using TashdateBot.Application.Processing;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Matches;

namespace TashdateBot.Application.UseCases;

/// <summary>Deletes everything stored about one person: profile, actions, raw notification, photo.</summary>
public sealed record ForgetMatchCommand(Guid MatchId) : ICommand<Unit>;

public sealed class ForgetMatchCommandValidator : AbstractValidator<ForgetMatchCommand>
{
    public ForgetMatchCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}

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
                .Bind(() => inbox.RemoveAsync(match.SourceMessageId, ct))
                .Bind(() => unitOfWork.CommitAsync(ct)))
            .Map(() => Unit.Value);

    private Task<UnitResult<Error>> DeletePhotoAsync(Match match, CancellationToken ct) =>
        match.Profile.Photo.HasValue
            ? photos.DeleteAsync(match.Profile.Photo.GetValueOrThrow(), ct)
            : Task.FromResult(UnitResult.Success<Error>());
}

public sealed record PurgeExpiredDataCommand : ICommand<PurgeReport>;

public sealed record PurgeReport(int Matches, int InboxMessages);

/// <summary>Retention: removes data older than <see cref="ProcessingOptions.RetentionDays"/> (0 = keep forever).</summary>
internal sealed class PurgeExpiredDataHandler(
    IMatchRepository matches,
    IInboxRepository inbox,
    IPhotoStore photos,
    IClock clock,
    IOptions<ProcessingOptions> options)
    : ICommandHandler<PurgeExpiredDataCommand, PurgeReport>
{
    public async Task<Result<PurgeReport, Error>> HandleAsync(PurgeExpiredDataCommand command, CancellationToken ct)
    {
        if (options.Value.RetentionDays == 0)
        {
            return new PurgeReport(0, 0);
        }

        var before = clock.UtcNow.AddDays(-options.Value.RetentionDays);
        return await matches.PurgeReceivedBeforeAsync(before, ct)
            .Tap(async purgedPhotos =>
            {
                foreach (var photo in purgedPhotos)
                {
                    await photos.DeleteAsync(photo, ct);
                }
            })
            .Bind(purgedPhotos => inbox.PurgeSentBeforeAsync(before, ct)
                .Map(inboxCount => new PurgeReport(purgedPhotos.Count, inboxCount)));
    }
}
