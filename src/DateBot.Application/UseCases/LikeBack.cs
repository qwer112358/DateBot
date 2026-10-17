using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

/// <summary>Presses the like-back button under the notification. The action is persisted as Pending first.</summary>
public sealed class LikeBack(
    ITashdateClient client,
    IUnitOfWork unitOfWork,
    ActionPacer pacer,
    IClock clock,
    IOptions<ProcessingOptions> options,
    ILogger<LikeBack> logger)
{
    public Task<UnitResult<Error>> ExecuteAsync(Match match, CancellationToken ct)
    {
        var resuming = match.PendingAction.HasValue && match.PendingAction.GetValueOrThrow().Type == ActionType.LikeBack;
        return pacer.BeforeActionAsync(ct)
            .Bind(() => match.StartLikeBack(clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Bind(() => PressAsync(match, resuming, ct))
            .Bind(() => match.CompleteLikeBack(clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct));
    }

    /// <summary>
    /// A like left Pending by a broken connection may already have been pressed; bots usually remove the buttons
    /// after a press, so a missing button on the retry means the like went through.
    /// </summary>
    private async Task<UnitResult<Error>> PressAsync(Match match, bool resuming, CancellationToken ct)
    {
        var pressed = await client.PressButtonAsync(match.SourceMessageId, [.. options.Value.LikeBackButtons], ct);
        if (pressed.IsFailure && resuming && pressed.Error.Code == TelegramErrors.ButtonNotFoundCode)
        {
            logger.LogInformation("Like button for {Name} is gone after an interrupted press: counting the like as done", match.Profile.Name);
            return UnitResult.Success<Error>();
        }

        return pressed;
    }
}
