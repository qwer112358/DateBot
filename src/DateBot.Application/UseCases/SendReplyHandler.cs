using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Notifications;
using DateBot.Application.Ports;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases;

internal sealed class SendReplyHandler(
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    GenerateAndSendReply reply,
    IOwnerNotifier notifier,
    IClock clock)
    : ICommandHandler<SendReplyCommand, DraftDecision>
{
    public Task<Result<DraftDecision, Error>> HandleAsync(SendReplyCommand command, CancellationToken ct) =>
        MatchId.Create(command.MatchId)
            .Bind(id => matches.FindByIdAsync(id, ct).Bind(found => found.ToResult(MatchErrors.NotFound(id))))
            .Bind(match => command.Approve ? SendAsync(match, ct) : SkipAsync(match, ct));

    private Task<Result<DraftDecision, Error>> SendAsync(Match match, CancellationToken ct) =>
        reply.SendDraftAsync(match, ct)
            .Map(_ => DraftDecision.Sent)
            .TapError(error => ErrorClassifier.IsRetryable(error) ? OfferDraftAgainAsync(match, ct) : FailAsync(match, error, ct));

    private Task<Result<DraftDecision, Error>> SkipAsync(Match match, CancellationToken ct) =>
        match.SkipReply(clock.UtcNow)
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => DraftDecision.Skipped);

    private Task FailAsync(Match match, Error error, CancellationToken ct) =>
        match.Fail(error, clock.UtcNow).Bind(() => unitOfWork.CommitAsync(ct));

    /// <summary>
    /// The buttons of the old card were removed on press; a temporary failure (pause, hourly limit, FLOOD_WAIT)
    /// would otherwise leave the draft without any way to send it.
    /// </summary>
    private Task OfferDraftAgainAsync(Match match, CancellationToken ct) =>
        match.DraftMessage.HasValue
            ? notifier.NotifyAsync(
                new DraftReadyNotification(match.Id, match.Kind, match.Profile, match.DraftMessage.GetValueOrThrow().Text.GetValueOrThrow()),
                ct)
            : Task.CompletedTask;
}
