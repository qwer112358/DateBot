using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Events;
using DateBot.Application.Notifications;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Processing.Options;
using DateBot.Application.Processing.Services;
using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.Events;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.EventHandlers;

/// <summary>Turns domain events into messages from the notifier bot.</summary>
internal sealed class NotifyOwner(IOwnerNotifier notifier, IOptions<ProcessingOptions> options) : IDomainEventHandler
{
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        ToNotification(domainEvent).HasValue
            ? notifier.NotifyAsync(ToNotification(domainEvent).GetValueOrThrow(), ct)
            : Task.CompletedTask;

    private Maybe<OwnerNotification> ToNotification(IDomainEvent domainEvent) => domainEvent switch
    {
        MessageSent { Kind: MatchKind.LikedMe } sent =>
            new LikeHandledNotification(sent.MatchId, sent.Profile, sent.LikedBack, sent.Text, sent.Channel),
        MessageSent sent => new MutualHandledNotification(sent.MatchId, sent.Profile, sent.Text, sent.Channel),
        LikeBackProposed proposed => new LikeBackProposedNotification(proposed.MatchId, proposed.Profile),
        LikeBackSent { Mutual: false } liked when options.Value.LikedMeMessage == LikedMeMessagePolicy.OnMutual =>
            new LikedBackNotification(liked.MatchId, liked.Profile),
        ReplyDrafted drafted => new DraftReadyNotification(
            drafted.MatchId,
            drafted.Kind,
            drafted.Profile,
            drafted.Text,
            drafted.Mutual,
            MessageRouter.Available(drafted.Profile, drafted.Mutual, drafted.Anchor, options.Value)),
        ProcessingFailed failed => new ActionFailedNotification(failed.MatchId, failed.Profile, failed.Reason),
        _ => Maybe<OwnerNotification>.None,
    };
}
