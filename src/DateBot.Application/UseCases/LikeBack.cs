using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

/// <summary>Presses the like-back button under the notification. The action is persisted as Pending first.</summary>
public sealed class LikeBack(
    ITashdateClient client,
    IUnitOfWork unitOfWork,
    ActionPacer pacer,
    IClock clock,
    IOptions<ProcessingOptions> options)
{
    public Task<UnitResult<Error>> ExecuteAsync(Match match, CancellationToken ct) =>
        pacer.BeforeActionAsync(ct)
            .Bind(() => match.StartLikeBack(clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Bind(() => client.PressButtonAsync(match.SourceMessageId, [.. options.Value.LikeBackButtons], ct))
            .Bind(() => match.CompleteLikeBack(clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct));
}
