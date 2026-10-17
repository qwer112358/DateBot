using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using TashdateBot.Application.Abstractions;
using TashdateBot.Application.Notifications;
using TashdateBot.Application.Ports;
using TashdateBot.Application.Processing;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Matches;

namespace TashdateBot.Application.UseCases;

/// <summary>Turns domain events into messages from the notifier bot.</summary>
internal sealed class NotifyOwner(IOwnerNotifier notifier, IOptions<ProcessingOptions> options) : IDomainEventHandler
{
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        ToNotification(domainEvent).HasValue
            ? notifier.NotifyAsync(ToNotification(domainEvent).GetValueOrThrow(), ct)
            : Task.CompletedTask;

    private Maybe<OwnerNotification> ToNotification(IDomainEvent domainEvent) => domainEvent switch
    {
        MessageSent { Kind: MatchKind.LikedMe } sent => new LikeHandledNotification(sent.MatchId, sent.Profile, sent.LikedBack, sent.Text),
        MessageSent sent => new MutualHandledNotification(sent.MatchId, sent.Profile, sent.Text),
        LikeBackSent liked when options.Value.LikedMeMessage == LikedMeMessagePolicy.OnMutual =>
            new LikedBackNotification(liked.MatchId, liked.Profile),
        ReplyDrafted drafted => new DraftReadyNotification(drafted.MatchId, drafted.Kind, drafted.Profile, drafted.Text),
        ProcessingFailed failed => new ActionFailedNotification(failed.MatchId, failed.Profile, failed.Reason),
        _ => Maybe<OwnerNotification>.None,
    };
}
