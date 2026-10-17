using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

/// <summary>
/// Likes the person back: through the app for a like found there, otherwise by the like button under the bot
/// notification. The action is persisted as Pending first.
/// </summary>
public sealed class LikeBack(
    ITashdateClient client,
    ITashdateApp app,
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
            .Bind(() => match.SourceMessageId.HasValue ? PressAsync(match, resuming, ct) : LikeInAppAsync(match, resuming, ct))
            .Bind(() => match.CompleteLikeBack(clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct));
    }

    /// <summary>
    /// A like left Pending by a broken connection may already have been pressed; bots usually remove the buttons
    /// after a press, so a missing button on the retry means the like went through.
    /// </summary>
    private async Task<UnitResult<Error>> PressAsync(Match match, bool resuming, CancellationToken ct)
    {
        var pressed = await client.PressButtonAsync(match.SourceMessageId.GetValueOrThrow(), [.. options.Value.LikeBackButtons], ct);
        if (pressed.IsFailure && resuming && pressed.Error.Code == TelegramErrors.ButtonNotFoundCode)
        {
            logger.LogInformation("Like button for {Name} is gone after an interrupted press: counting the like as done", match.Profile.Name);
            return UnitResult.Success<Error>();
        }

        return pressed;
    }

    /// <summary>
    /// The app answers a like with "match" and the username right away. An interrupted like is first looked up among
    /// the matches: once it went through, the person has left the likes list and is a match.
    /// </summary>
    private Task<UnitResult<Error>> LikeInAppAsync(Match match, bool resuming, CancellationToken ct) =>
        match.Profile.AppId
            .ToResult(MatchErrors.LikeBackNotAllowed)
            .Bind(appId => (resuming ? FindMatchAsync(appId, ct) : Task.FromResult(Result.Success<Maybe<AppLikeResult>, Error>(Maybe<AppLikeResult>.None)))
                .Bind(found => found.HasValue
                    ? Task.FromResult(Result.Success<AppLikeResult, Error>(found.GetValueOrThrow()))
                    : app.LikeAsync(appId, ct)))
            .Bind(result => Remember(match, result));

    private Task<Result<Maybe<AppLikeResult>, Error>> FindMatchAsync(AppProfileId appId, CancellationToken ct) =>
        app.GetMatchesAsync(ct).Map(matches => matches
            .TryFirst(m => m.ProfileId == appId.Value)
            .Map(m => new AppLikeResult(Mutual: true, m.Username)));

    /// <summary>An unusable username from the app is dropped: the in-app chat still works without it.</summary>
    private static UnitResult<Error> Remember(Match match, AppLikeResult result)
    {
        var username = result.Username
            .Map(ContactUsername.Create)
            .Bind(created => created.IsSuccess ? Maybe.From(created.Value) : Maybe<ContactUsername>.None);
        return match.RevealContact(username)
            .Bind(() => result.Mutual ? match.ConfirmMutual() : UnitResult.Success<Error>());
    }
}
