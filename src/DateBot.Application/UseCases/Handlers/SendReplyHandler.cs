using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.UseCases.Handlers;

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
            .Bind(match => command.Approve
                ? SendAsync(match, command.Channel is { } channel ? channel : Maybe<MessageChannel>.None, ct)
                : SkipAsync(match, ct));

    /// <summary>
    /// Only a send that may have gone out fails the match. Anything else left the draft unsent (pause, limit, the
    /// person closed Telegram messages): the card comes again so you can retry or pick the other channel.
    /// </summary>
    private Task<Result<DraftDecision, Error>> SendAsync(Match match, Maybe<MessageChannel> channel, CancellationToken ct) =>
        reply.SendDraftAsync(match, channel, ct)
            .Map(_ => DraftDecision.Sent)
            .TapError(error => match.DraftMessage.HasValue ? OfferDraftAgainAsync(match, ct) : FailAsync(match, error, ct));

    private Task<Result<DraftDecision, Error>> SkipAsync(Match match, CancellationToken ct) =>
        match.SkipReply(clock.UtcNow)
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => DraftDecision.Skipped);

    private Task FailAsync(Match match, Error error, CancellationToken ct) =>
        match.IsClosed ? Task.CompletedTask : match.Fail(error, clock.UtcNow).Bind(() => unitOfWork.CommitAsync(ct));

    /// <summary>The buttons of the old card were removed on press; without a new card the draft could not be sent at all.</summary>
    private Task OfferDraftAgainAsync(Match match, CancellationToken ct) =>
        reply.DraftCard(match).HasValue ? notifier.NotifyAsync(reply.DraftCard(match).GetValueOrThrow(), ct) : Task.CompletedTask;
}
