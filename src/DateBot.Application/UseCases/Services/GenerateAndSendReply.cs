using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Errors;
using DateBot.Application.Notifications;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Processing.Options;
using DateBot.Application.Processing.Routes;
using DateBot.Application.Processing.Services;
using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Models;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Services;
using DateBot.Application.Telegram.Models;
using DateBot.Application.UseCases.Enums;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Services;

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
    ProfilePhotos photos,
    IUnitOfWork unitOfWork,
    ActionPacer pacer,
    IClock clock,
    IOptions<ProcessingOptions> processing,
    IOptions<ReplyOptions> replies,
    ILogger<GenerateAndSendReply> logger)
{
    /// <param name="anchor">The bot notification that triggered the message (bot-chat channel); none for an app like.</param>
    /// <param name="draftOnly">Keep the text as a draft whatever the mode: you asked to see it, not to send it.</param>
    public async Task<Result<ReplyOutcome, Error>> ExecuteAsync(Match match, Maybe<BotMessageId> anchor, CancellationToken ct, bool draftOnly = false)
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

        var enriched = await enricher.EnrichAsync(match, ct);
        if (enriched.IsFailure)
        {
            logger.LogWarning("App profile of {Name} unavailable, using the notification only: {Error}", match.Profile.Name, enriched.Error);
        }

        var channels = MessageRouter.Available(match, anchor, processing.Value);
        if (channels.IsEmpty && !match.IsMutual)
        {
            logger.LogInformation("No channel reaches {Name} before the match; the message waits for it", match.Profile.Name);
            return ReplyOutcome.NotNeeded;
        }

        // A match no channel reaches (no @username, not found in the app) still gets its text: as a draft you send by hand.
        return await LoadPhotoAsync(match, ct)
            .Bind(photos => composer.ComposeAsync(match.Profile, photos, ct))
            .Bind(composed => draftOnly || ShouldDraft(composed) || channels.IsEmpty
                ? DraftAsync(match, composed.Text, anchor, ct)
                : MessageRouter.Choose(match, anchor, processing.Value).Bind(route => SendAsync(match, composed.Text, route, ct)));
    }

    /// <summary>
    /// "Find in the app" on a draft no channel could reach: looks the person up in the app again (it may have been
    /// down, or the match not listed yet) and returns the channels that reach them now.
    /// </summary>
    public async Task<Result<ImmutableArray<MessageChannel>, Error>> LookUpAsync(Match match, CancellationToken ct)
    {
        if (match.DraftMessage.HasNoValue)
        {
            return MatchErrors.NoDraft;
        }

        return await enricher.EnrichAsync(match, ct)
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => ChannelsFor(match));
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

    /// <summary>The card of the match's current draft, for when the old card lost its buttons on a press.</summary>
    public Maybe<DraftReadyNotification> DraftCard(Match match) =>
        match.DraftMessage.Map(draft => new DraftReadyNotification(
            match.Id, match.Kind, match.Profile, draft.Text.GetValueOrThrow(), match.IsMutual, ChannelsFor(match)));

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

    /// <summary>
    /// The photos are always fetched and kept, because the owner's notifications show them; the LLM gets them all only
    /// when <see cref="ReplyOptions.UsePhoto"/> allows.
    /// </summary>
    private async Task<Result<IReadOnlyList<ProfilePhoto>, Error>> LoadPhotoAsync(Match match, CancellationToken ct)
    {
        var all = await photos.GetAllAsync(match, ct);
        return Result.Success<IReadOnlyList<ProfilePhoto>, Error>(replies.Value.UsePhoto ? all : []);
    }
}
