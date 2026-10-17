using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DateBot.Application.Abstractions;
using DateBot.Application.Notifications;
using DateBot.Application.Parsing;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;
using DateBot.Domain.Sync;

namespace DateBot.Application.UseCases;

public sealed record ProcessBotMessageCommand(RawBotMessage Message) : ICommand<ProcessOutcome>;

public enum ProcessOutcome
{
    AlreadyProcessed,
    Done,
    AwaitingConfirmation,
    Ignored,
    ParseFailed,
    Failed,
}

public sealed class ProcessBotMessageCommandValidator : AbstractValidator<ProcessBotMessageCommand>
{
    public ProcessBotMessageCommandValidator()
    {
        RuleFor(c => c.Message.Id).GreaterThan(0);
        RuleFor(c => c.Message.Text).NotNull().MaximumLength(InboxMessage.MaxRawTextLength - 1000);
        RuleFor(c => c.Message.SentAt).NotEqual(default(DateTimeOffset));
    }
}

/// <summary>
/// One bot message end to end: store raw → parse → find or create the match → plan → like back / message →
/// final status and cursor in one transaction. Retryable errors (FLOOD_WAIT, hourly limit, /stop, network) leave the
/// message unfinished so it is picked up again; other errors fail the match and move the cursor on.
/// </summary>
internal sealed class ProcessBotMessageHandler(
    IInboxRepository inbox,
    ISyncCursorRepository cursors,
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    IOwnerNotifier notifier,
    IClock clock,
    ParsingRules rules,
    LikeBack likeBack,
    GenerateAndSendReply reply,
    IOptions<ProcessingOptions> options,
    ILogger<ProcessBotMessageHandler> logger)
    : ICommandHandler<ProcessBotMessageCommand, ProcessOutcome>
{
    public Task<Result<ProcessOutcome, Error>> HandleAsync(ProcessBotMessageCommand command, CancellationToken ct) =>
        BotMessageId.Create(command.Message.Id)
            .Bind(id => LoadContextAsync(id, command.Message, ct))
            .Bind(context => context.IsProcessed
                ? AlignCursorAsync(context, ct).Map(() => ProcessOutcome.AlreadyProcessed)
                : RouteAsync(context, ct));

    /// <summary>A resolved message must never stay ahead of the cursor, or catch-up would loop on it.</summary>
    private Task<UnitResult<Error>> AlignCursorAsync(MessageContext context, CancellationToken ct) =>
        context.Cursor.IsProcessed(context.Id)
            ? Task.FromResult(UnitResult.Success<Error>())
            : context.Cursor.Advance(context.Id, clock.UtcNow)
                .Bind(() => cursors.SaveAsync(context.Cursor, ct))
                .Bind(() => unitOfWork.CommitAsync(ct));

    private Task<Result<MessageContext, Error>> LoadContextAsync(BotMessageId id, RawBotMessage raw, CancellationToken ct) =>
        cursors.GetAsync(ct)
            .Bind(cursor => inbox.FindAsync(id, ct)
                .Bind(found => found.HasValue
                    ? Task.FromResult(Result.Success<MessageContext, Error>(new MessageContext(id, raw, found.GetValueOrThrow(), cursor)))
                    : ReceiveAsync(id, raw, cursor, ct)));

    /// <summary>Stage 1 requirement: the raw message is stored before anything else happens.</summary>
    private Task<Result<MessageContext, Error>> ReceiveAsync(BotMessageId id, RawBotMessage raw, SyncCursor cursor, CancellationToken ct) =>
        InboxMessage.Receive(id, raw.ToArchiveText(), raw.SentAt, clock.UtcNow)
            .Bind(message => inbox.AddAsync(message, ct)
                .Bind(() => unitOfWork.CommitAsync(ct))
                .Map(() => new MessageContext(id, raw, message, cursor)));

    private Task<Result<ProcessOutcome, Error>> RouteAsync(MessageContext context, CancellationToken ct) =>
        BotNotificationParser.Parse(context.Raw, rules) switch
        {
            LikeNotification like => HandleProfileAsync(context, MatchKind.LikedMe, like.Profile, ct),
            MutualNotification mutual => HandleProfileAsync(context, MatchKind.Mutual, mutual.Profile, ct),
            IgnoredNotification ignored => IgnoreAsync(context, ignored.Reason, ct),
            UnknownNotification unknown => ParseFailedAsync(context, unknown.Reason, ct),
            var other => ParseFailedAsync(context, $"Неподдержанный тип уведомления {other.GetType().Name}", ct),
        };

    private Task<Result<ProcessOutcome, Error>> HandleProfileAsync(
        MessageContext context, MatchKind kind, ParsedProfile parsed, CancellationToken ct) =>
        ProfileMapper.ToDomain(parsed, context.Raw.Text) switch
        {
            { IsFailure: true } failed => ParseFailedAsync(context, failed.Error.Message, ct),
            { Value: var mapped } when !ProfileFilter.Accepts(mapped.Profile, options.Value.Filters) =>
                IgnoreAsync(context, $"Анкета {mapped.Profile.Name} не прошла фильтры", ct),
            { Value: var mapped } => matches
                .FindByPersonAsync(mapped.PersonId, ct)
                .Bind(existing => ExecuteAsync(context, kind, mapped, existing, ct)),
        };

    private Task<Result<ProcessOutcome, Error>> ExecuteAsync(
        MessageContext context, MatchKind kind, MappedProfile mapped, Maybe<Match> existing, CancellationToken ct) =>
        ScenarioPlanner.Plan(kind, existing, context.Id, options.Value.LikedMeMessage) switch
        {
            ExecutePlan plan => GetOrCreateAsync(context, kind, mapped, existing, ct)
                .Bind(match => RunStepsAsync(context, kind, match, plan, ct)),
            IgnorePlan ignore => IgnoreAsync(context, ignore.Reason, ct),
            var other => ParseFailedAsync(context, $"Неизвестный план {other.GetType().Name}", ct),
        };

    private Task<Result<Match, Error>> GetOrCreateAsync(
        MessageContext context, MatchKind kind, MappedProfile mapped, Maybe<Match> existing, CancellationToken ct) =>
        existing.HasValue
            ? Task.FromResult(existing.GetValueOrThrow().RevealContact(mapped.Profile.Username).Map(() => existing.GetValueOrThrow()))
            : MatchId.Create(Guid.CreateVersion7(clock.UtcNow))
                .Bind(id => Match.Create(new MatchInput(id, mapped.PersonId, context.Id, kind, mapped.Profile, clock.UtcNow)))
                .Bind(match => matches.AddAsync(match, ct)
                    .Bind(() => unitOfWork.CommitAsync(ct))
                    .Map(() => match));

    private async Task<Result<ProcessOutcome, Error>> RunStepsAsync(
        MessageContext context, MatchKind trigger, Match match, ExecutePlan plan, CancellationToken ct)
    {
        var steps = await (plan.LikeBack ? likeBack.ExecuteAsync(match, ct) : Task.FromResult(UnitResult.Success<Error>()))
            .Bind(() => plan.Message
                ? reply.ExecuteAsync(match, trigger, context.Id, ct)
                : Task.FromResult(Result.Success<ReplyOutcome, Error>(ReplyOutcome.NotNeeded)));

        return await steps.Match(
            outcome => FinishAsync(context, InboxStatus.Done, Maybe<string>.None, ct)
                .Map(() => outcome == ReplyOutcome.Drafted ? ProcessOutcome.AwaitingConfirmation : ProcessOutcome.Done),
            error => ErrorClassifier.IsRetryable(error)
                ? Task.FromResult(Result.Failure<ProcessOutcome, Error>(error))
                : FailAsync(context, match, error, ct));
    }

    private Task<Result<ProcessOutcome, Error>> FailAsync(MessageContext context, Match match, Error error, CancellationToken ct)
    {
        logger.LogWarning("Message {MessageId} for {Name} failed: {Error}", context.Id, match.Profile.Name, error);
        var failed = match.IsClosed ? UnitResult.Success<Error>() : match.Fail(error, clock.UtcNow);
        return failed
            .Bind(() => FinishAsync(context, InboxStatus.Failed, error.ToString(), ct))
            .Map(() => ProcessOutcome.Failed);
    }

    private Task<Result<ProcessOutcome, Error>> IgnoreAsync(MessageContext context, string reason, CancellationToken ct)
    {
        logger.LogInformation("Message {MessageId} ignored: {Reason}", context.Id, reason);
        return FinishAsync(context, InboxStatus.Ignored, reason, ct).Map(() => ProcessOutcome.Ignored);
    }

    private Task<Result<ProcessOutcome, Error>> ParseFailedAsync(MessageContext context, string reason, CancellationToken ct)
    {
        logger.LogWarning("Message {MessageId} not parsed: {Reason}", context.Id, reason);
        return FinishAsync(context, InboxStatus.ParseFailed, reason, ct)
            .Tap(() => notifier.NotifyAsync(new ParseFailedNotification(context.Id.Value, context.Raw.ToArchiveText(), reason), ct))
            .Map(() => ProcessOutcome.ParseFailed);
    }

    /// <summary>Final status of the message and the cursor move go into the same transaction.</summary>
    private Task<UnitResult<Error>> FinishAsync(MessageContext context, InboxStatus status, Maybe<string> note, CancellationToken ct) =>
        context.Inbox.Resolve(status, note, clock.UtcNow)
            .Bind(() => context.Cursor.Advance(context.Id, clock.UtcNow))
            .Bind(() => cursors.SaveAsync(context.Cursor, ct))
            .Bind(() => unitOfWork.CommitAsync(ct));

    private sealed record MessageContext(BotMessageId Id, RawBotMessage Raw, InboxMessage Inbox, SyncCursor Cursor)
    {
        public bool IsProcessed => Inbox.IsResolved || Cursor.IsProcessed(Id);
    }
}
