using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using TashdateBot.Application.Ports;
using TashdateBot.Application.Processing;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Matches;

namespace TashdateBot.Application.UseCases;

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
