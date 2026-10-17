using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Application.Replies;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

/// <summary>
/// Downloads the photo, writes the message (LLM or template), then either sends it or, in Confirm mode,
/// keeps it as a draft for the owner. At most one auto-message per person is enforced by the aggregate.
/// </summary>
public sealed class GenerateAndSendReply(
    ITashdateClient client,
    ReplyComposer composer,
    IPhotoStore photos,
    IUnitOfWork unitOfWork,
    ActionPacer pacer,
    IClock clock,
    IOptions<ProcessingOptions> processing,
    IOptions<ReplyOptions> replies,
    ILogger<GenerateAndSendReply> logger)
{
    /// <param name="trigger">Kind of the notification that triggered the message.</param>
    /// <param name="notificationId">That notification; the anchor for the ViaBot route.</param>
    public Task<Result<ReplyOutcome, Error>> ExecuteAsync(Match match, MatchKind trigger, BotMessageId notificationId, CancellationToken ct)
    {
        // A send left Pending by a crash may or may not have reached Telegram: never risk a second message.
        if (match.PendingAction.HasValue && match.PendingAction.GetValueOrThrow().Type == ActionType.SendMessage)
        {
            return Task.FromResult(Result.Failure<ReplyOutcome, Error>(MatchErrors.SendOutcomeUnknown));
        }

        if (!match.CanStartMessage)
        {
            return Task.FromResult(Result.Success<ReplyOutcome, Error>(ReplyOutcome.NotNeeded));
        }

        var route = MessageRouter.Choose(match.Profile, trigger == MatchKind.Mutual, notificationId, processing.Value);
        return LoadPhotoAsync(match, ct)
            .Bind(photo => composer.ComposeAsync(match.Profile, photo, ct))
            .Bind(composed => ShouldDraft(composed)
                ? DraftAsync(match, composed.Text, notificationId, ct)
                : SendAsync(match, composed.Text, route, ct));
    }

    /// <summary>
    /// The owner pressed "Send" on a draft: the message goes against the notification the draft answers.
    /// A draft anchored to a later notification than the match's first one was triggered by the mutual like.
    /// </summary>
    public Task<Result<ReplyOutcome, Error>> SendDraftAsync(Match match, CancellationToken ct) =>
        match.DraftMessage
            .ToResult(MatchErrors.NoDraft)
            .Bind(draft =>
            {
                var anchor = draft.Anchor.GetValueOrDefault(match.SourceMessageId);
                var mutual = match.Kind == MatchKind.Mutual || anchor != match.SourceMessageId;
                var route = MessageRouter.Choose(match.Profile, mutual, anchor, processing.Value);
                return SendAsync(match, draft.Text.GetValueOrThrow(), route, ct);
            });

    private bool ShouldDraft(ComposedReply composed) =>
        processing.Value.Mode == ProcessingMode.Confirm
        || (composed.Rejection.HasValue && replies.Value.InvalidReplyPolicy == InvalidReplyPolicy.Confirm);

    private Task<Result<ReplyOutcome, Error>> DraftAsync(Match match, ReplyText text, BotMessageId anchor, CancellationToken ct) =>
        match.DraftReply(text, anchor, clock.UtcNow)
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => ReplyOutcome.Drafted);

    private Task<Result<ReplyOutcome, Error>> SendAsync(Match match, ReplyText text, MessageRoute route, CancellationToken ct) =>
        pacer.BeforeActionAsync(ct)
            .Bind(() => match.StartMessage(text, clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Bind(() => DeliverAsync(route, text, ct).TapError(error => ReleaseIfNotSentAsync(match, error, ct)))
            .Bind(() => match.CompleteMessage(clock.UtcNow))
            .Bind(() => match.Close())
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => ReplyOutcome.Sent);

    private Task<UnitResult<Error>> DeliverAsync(MessageRoute route, ReplyText text, CancellationToken ct) => route switch
    {
        DirectRoute direct => client.SendToUserAsync(direct.Username, text, ct),
        // Plain text goes to whatever profile the bot shows now, so it must still be the anchor.
        ViaBotRoute { ButtonLabels.IsDefaultOrEmpty: true } viaBot => client.EnsureLatestAsync(viaBot.Anchor, ct)
            .Bind(() => client.SendToBotAsync(text, ct)),
        ViaBotRoute viaBot => client.PressButtonAsync(viaBot.Anchor, viaBot.ButtonLabels, ct)
            .Tap(() => clock.DelayAsync(processing.Value.ButtonToTextPause, ct))
            .Bind(() => client.SendToBotAsync(text, ct)),
        _ => Task.FromResult(UnitResult.Failure(Error.Unexpected("reply.route", $"Неизвестный маршрут {route.GetType().Name}"))),
    };

    /// <summary>
    /// Telegram refused the send or it never left, so nothing went out: release the pending action for a later retry.
    /// When the outcome is unknown the action stays Pending and the match fails: at most one message.
    /// </summary>
    private async Task ReleaseIfNotSentAsync(Match match, Error error, CancellationToken ct)
    {
        if (error.Code == TelegramErrors.SendOutcomeUnknownCode)
        {
            logger.LogWarning("Send to {Name} may have been delivered, not retrying: {Error}", match.Profile.Name, error);
            return;
        }

        match.AbortPendingAction(error, clock.UtcNow);
        var saved = await unitOfWork.CommitAsync(ct);
        if (saved.IsFailure)
        {
            logger.LogError("Could not release pending send for {MatchId}: {Error}", match.Id, saved.Error);
        }
    }

    /// <summary>Photo from local storage or downloaded from Telegram; problems with it never block the message.</summary>
    private async Task<Result<Maybe<ProfilePhoto>, Error>> LoadPhotoAsync(Match match, CancellationToken ct)
    {
        if (!replies.Value.UsePhoto || match.Profile.Photo.HasNoValue)
        {
            return Maybe<ProfilePhoto>.None;
        }

        var photoId = match.Profile.Photo.GetValueOrThrow();
        var photo = await photos.LoadAsync(photoId, ct)
            .Bind(stored => stored.HasValue
                ? Task.FromResult(Result.Success<Maybe<ProfilePhoto>, Error>(stored))
                : DownloadAsync(match.SourceMessageId, photoId, ct));

        return photo.IsSuccess ? photo : LogPhotoFailure(match, photo.Error);
    }

    private Task<Result<Maybe<ProfilePhoto>, Error>> DownloadAsync(BotMessageId messageId, PhotoFileId photoId, CancellationToken ct) =>
        client.DownloadPhotoAsync(messageId, ct)
            .Tap(downloaded => downloaded.HasValue
                ? photos.SaveAsync(photoId, downloaded.GetValueOrThrow(), ct)
                : Task.CompletedTask);

    private Result<Maybe<ProfilePhoto>, Error> LogPhotoFailure(Match match, Error error)
    {
        logger.LogWarning("Photo of {Name} unavailable, writing without it: {Error}", match.Profile.Name, error);
        return Maybe<ProfilePhoto>.None;
    }
}
