using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Parsing;
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
/// Completes the profile from the app, downloads the photo, writes the message (LLM or template), then either sends it
/// through the preferred channel or, in Confirm mode, keeps it as a draft for the owner, who also picks the channel.
/// At most one auto-message per person is enforced by the aggregate.
/// </summary>
public sealed class GenerateAndSendReply(
    ITashdateClient client,
    ITashdateApp app,
    EnrichFromApp enricher,
    ReplyComposer composer,
    IPhotoStore photos,
    IUnitOfWork unitOfWork,
    ActionPacer pacer,
    IClock clock,
    IOptions<ProcessingOptions> processing,
    IOptions<ReplyOptions> replies,
    ILogger<GenerateAndSendReply> logger)
{
    /// <param name="anchor">The bot notification that triggered the message (bot-chat channel); none for an app like.</param>
    public async Task<Result<ReplyOutcome, Error>> ExecuteAsync(Match match, Maybe<BotMessageId> anchor, CancellationToken ct)
    {
        // A send left Pending by a crash may or may not have reached the person: never risk a second message.
        if (match.PendingAction.HasValue && match.PendingAction.GetValueOrThrow().Type == ActionType.SendMessage)
        {
            return MatchErrors.SendOutcomeUnknown;
        }

        if (!match.CanStartMessage || (!match.IsMutual && processing.Value.LikedMeMessage == LikedMeMessagePolicy.OnMutual))
        {
            return ReplyOutcome.NotNeeded;
        }

        await enricher.EnrichAsync(match, ct);
        var channels = MessageRouter.Available(match, anchor, processing.Value);
        if (channels.IsEmpty)
        {
            logger.LogInformation("No channel reaches {Name} yet (no match or no contact); the message waits", match.Profile.Name);
            return ReplyOutcome.NotNeeded;
        }

        return await LoadPhotoAsync(match, ct)
            .Bind(photo => composer.ComposeAsync(match.Profile, photo, ct))
            .Bind(composed => ShouldDraft(composed)
                ? DraftAsync(match, composed.Text, anchor, ct)
                : MessageRouter.Choose(match, anchor, processing.Value).Bind(route => SendAsync(match, composed.Text, route, ct)));
    }

    /// <summary>
    /// The owner pressed a channel button on a draft (or plain "Send": the preferred channel). The bot-chat channel
    /// answers the notification the draft was made for.
    /// </summary>
    public Task<Result<ReplyOutcome, Error>> SendDraftAsync(Match match, Maybe<MessageChannel> channel, CancellationToken ct) =>
        match.DraftMessage
            .ToResult(MatchErrors.NoDraft)
            .Bind(draft => channel.HasValue
                ? MessageRouter.Route(channel.GetValueOrThrow(), match, draft.Anchor, processing.Value)
                : MessageRouter.Choose(match, draft.Anchor, processing.Value))
            .Bind(route => SendAsync(match, match.DraftMessage.GetValueOrThrow().Text.GetValueOrThrow(), route, ct));

    /// <summary>Channels the owner can pick for this match's draft.</summary>
    public ImmutableArray<MessageChannel> ChannelsFor(Match match) =>
        MessageRouter.Available(match, match.DraftMessage.Bind(d => d.Anchor), processing.Value);

    private bool ShouldDraft(ComposedReply composed) =>
        processing.Value.Mode == ProcessingMode.Confirm
        || (composed.Rejection.HasValue && replies.Value.InvalidReplyPolicy == InvalidReplyPolicy.Confirm);

    private Task<Result<ReplyOutcome, Error>> DraftAsync(Match match, ReplyText text, Maybe<BotMessageId> anchor, CancellationToken ct) =>
        match.DraftReply(text, anchor, clock.UtcNow)
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => ReplyOutcome.Drafted);

    private Task<Result<ReplyOutcome, Error>> SendAsync(Match match, ReplyText text, MessageRoute route, CancellationToken ct) =>
        pacer.BeforeActionAsync(ct)
            .Bind(() => match.StartMessage(text, route.Channel, clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Bind(() => DeliverAsync(route, text, ct).TapError(error => ReleaseIfNotSentAsync(match, error, ct)))
            .Bind(() => match.CompleteMessage(clock.UtcNow))
            .Bind(() => match.Close())
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => ReplyOutcome.Sent);

    private Task<UnitResult<Error>> DeliverAsync(MessageRoute route, ReplyText text, CancellationToken ct) => route switch
    {
        DirectRoute direct => client.SendToUserAsync(direct.Username, text, ct),
        AppChatRoute chat => app.SendMessageAsync(chat.To, text, chat.ClientKey, ct),
        // Plain text goes to whatever profile the bot shows now, so it must still be the anchor.
        ViaBotRoute { ButtonLabels.IsDefaultOrEmpty: true } viaBot => client.EnsureLatestAsync(viaBot.Anchor, ct)
            .Bind(() => client.SendToBotAsync(text, ct)),
        ViaBotRoute viaBot => client.PressButtonAsync(viaBot.Anchor, viaBot.ButtonLabels, ct)
            .Tap(() => clock.DelayAsync(processing.Value.ButtonToTextPause, ct))
            .Bind(() => client.SendToBotAsync(text, ct)),
        _ => Task.FromResult(UnitResult.Failure(Error.Unexpected("reply.route", $"Неизвестный маршрут {route.GetType().Name}"))),
    };

    /// <summary>
    /// The channel refused the send or it never left, so nothing went out: release the pending action for a later
    /// retry (an approved draft goes back to Draft). When the outcome is unknown the action stays Pending and the
    /// match fails: at most one message.
    /// </summary>
    private async Task ReleaseIfNotSentAsync(Match match, Error error, CancellationToken ct)
    {
        if (error.Code == DeliveryErrors.OutcomeUnknownCode)
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

    /// <summary>Photo from local storage or downloaded from Telegram or the app; problems with it never block the message.</summary>
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
                : DownloadAsync(match, photoId, ct));

        return photo.IsSuccess ? photo : LogPhotoFailure(match, photo.Error);
    }

    private Task<Result<Maybe<ProfilePhoto>, Error>> DownloadAsync(Match match, PhotoFileId photoId, CancellationToken ct)
    {
        var download = ProfileMapper.IsAppPhoto(photoId)
            ? processing.Value.MiniApp.Enabled ? app.DownloadPhotoAsync(photoId, ct) : NoPhoto()
            : match.SourceMessageId.HasValue ? client.DownloadPhotoAsync(match.SourceMessageId.GetValueOrThrow(), ct) : NoPhoto();
        return download.Tap(downloaded => downloaded.HasValue
            ? photos.SaveAsync(photoId, downloaded.GetValueOrThrow(), ct)
            : Task.CompletedTask);
    }

    private static Task<Result<Maybe<ProfilePhoto>, Error>> NoPhoto() =>
        Task.FromResult(Result.Success<Maybe<ProfilePhoto>, Error>(Maybe<ProfilePhoto>.None));

    private Result<Maybe<ProfilePhoto>, Error> LogPhotoFailure(Match match, Error error)
    {
        logger.LogWarning("Photo of {Name} unavailable, writing without it: {Error}", match.Profile.Name, error);
        return Maybe<ProfilePhoto>.None;
    }
}
