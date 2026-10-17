using System.Collections.Frozen;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.Events;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.Validators;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Domain.Matches;

/// <summary>
/// Aggregate root: one per person from Tashdate, whether they came from a bot notification or a like in the app.
/// Invariants: at most one auto-message per person; like back only for <see cref="MatchKind.LikedMe"/>;
/// status changes only along <c>Received → LikedBack → MessageSent → Done</c>, any open status → Failed.
/// </summary>
public sealed class Match : AggregateRoot<MatchId>
{
    private static readonly MatchInputValidator InputValidator = new();
    private static readonly MatchStateValidator StateValidator = new();

    private static readonly FrozenDictionary<MatchStatus, MatchStatus[]> AllowedTransitions =
        new Dictionary<MatchStatus, MatchStatus[]>
        {
            [MatchStatus.Received] = [MatchStatus.LikedBack, MatchStatus.MessageSent, MatchStatus.Skipped, MatchStatus.Failed],
            [MatchStatus.LikedBack] = [MatchStatus.MessageSent, MatchStatus.Skipped, MatchStatus.Failed],
            [MatchStatus.MessageSent] = [MatchStatus.Done, MatchStatus.Failed],
            [MatchStatus.Done] = [],
            [MatchStatus.Failed] = [],
            [MatchStatus.Skipped] = [],
        }.ToFrozenDictionary();

    private readonly List<MatchAction> _actions;

    private Match(
        MatchId id,
        TashdateUserId personId,
        Maybe<BotMessageId> sourceMessageId,
        MatchKind kind,
        Profile profile,
        DateTimeOffset receivedAt,
        MatchStatus status,
        IEnumerable<MatchAction> actions,
        bool isMutual)
        : base(id)
    {
        PersonId = personId;
        SourceMessageId = sourceMessageId;
        Kind = kind;
        Profile = profile;
        ReceivedAt = receivedAt;
        Status = status;
        IsMutual = isMutual;
        _actions = [.. actions];
    }

    public TashdateUserId PersonId { get; }

    /// <summary>The bot notification the person came from; none for a like found in the app.</summary>
    public Maybe<BotMessageId> SourceMessageId { get; }

    public MatchKind Kind { get; }

    public Profile Profile { get; private set; }

    public DateTimeOffset ReceivedAt { get; }

    public MatchStatus Status { get; private set; }

    /// <summary>Both sides liked each other: a mutual notification, or our like back answered with a match.</summary>
    public bool IsMutual { get; private set; }

    public IReadOnlyList<MatchAction> Actions => _actions;

    /// <summary>Every key the person is known by; tombstones are written for all of them.</summary>
    public ImmutableArray<string> IdentityKeys => PersonKeys.Of(PersonId, Profile);

    public bool IsClosed => Status is MatchStatus.Done or MatchStatus.Failed or MatchStatus.Skipped;

    public Maybe<MatchAction> PendingAction => _actions.TryFirst(a => a.Status == ActionStatus.Pending);

    public Maybe<MatchAction> DraftMessage =>
        _actions.TryFirst(a => a.Type == ActionType.SendMessage && a.Status == ActionStatus.Draft);

    public bool HasActiveMessage => _actions.Any(a =>
        a.Type == ActionType.SendMessage && a.Status is ActionStatus.Draft or ActionStatus.Pending or ActionStatus.Done);

    public bool HasLikedBack => _actions.Any(a => a.Type == ActionType.LikeBack && a.Status == ActionStatus.Done);

    public bool CanLikeBack => Kind == MatchKind.LikedMe && Status == MatchStatus.Received;

    public bool CanStartMessage =>
        !IsClosed
        && !HasActiveMessage
        && (Status == MatchStatus.LikedBack || (Status == MatchStatus.Received && Kind == MatchKind.Mutual));

    private int NextSequence => _actions.Count == 0 ? 1 : _actions.Max(a => a.Sequence) + 1;

    public static Result<Match, Error> Create(MatchInput input) =>
        InputValidator
            .ValidateAndCreate(input, "match.invalid", i => new Match(
                i.Id, i.PersonId, i.SourceMessageId, i.Kind, i.Profile, i.ReceivedAt, MatchStatus.Received, [], i.Kind == MatchKind.Mutual))
            .Tap(match => match.Raise(match.Kind == MatchKind.LikedMe
                ? new LikeReceived(match.Id, match.Profile, match.ReceivedAt)
                : new MutualMatchReceived(match.Id, match.Profile, match.ReceivedAt)));

    /// <summary>Rebuilds a stored match without raising events. Inconsistent data gives an error.</summary>
    public static Result<Match, Error> Restore(MatchState state) =>
        StateValidator.ValidateAndCreate(state, "match.corrupted", s => new Match(
            s.Id, s.PersonId, s.SourceMessageId, s.Kind, s.Profile, s.ReceivedAt, s.Status,
            s.Actions.OrderBy(a => a.Sequence).Select(MatchAction.Restore), s.IsMutual));

    /// <summary>A later notification or the app's like response showed a contact: remember it for messages.</summary>
    public UnitResult<Error> RevealContact(Maybe<ContactUsername> username, Maybe<AppProfileId> appId = default)
    {
        Profile = Profile.WithContact(username, appId);
        return UnitResult.Success<Error>();
    }

    /// <summary>Fills what the short notification lacked from the full profile (bio, interests, app id).</summary>
    public UnitResult<Error> Enrich(Profile details)
    {
        Profile = Profile.Enrich(details);
        return UnitResult.Success<Error>();
    }

    /// <summary>The person liked us back: channels that need a match become available.</summary>
    public UnitResult<Error> ConfirmMutual()
    {
        IsMutual = true;
        return UnitResult.Success<Error>();
    }

    /// <summary>A new person we do not answer (filters): kept so the app's like list does not bring them back.</summary>
    public UnitResult<Error> Decline() =>
        Guard.Require(_actions.Count == 0, MatchErrors.InvalidState(Status, "отказ от ответа"))
            .Bind(() => TransitionTo(MatchStatus.Skipped));

    public UnitResult<Error> StartLikeBack(DateTimeOffset now)
    {
        // A like back left Pending by a restart is simply repeated.
        if (PendingOf(ActionType.LikeBack).HasValue)
        {
            return UnitResult.Success<Error>();
        }

        return Guard.Require(Kind == MatchKind.LikedMe, MatchErrors.LikeBackNotAllowed)
            .Bind(() => Guard.Require(Status == MatchStatus.Received, MatchErrors.InvalidState(Status, "лайк в ответ")))
            .Bind(() => Guard.Require(PendingAction.HasNoValue, MatchErrors.ActionInProgress))
            .Tap(() => _actions.Add(MatchAction.Start(
                NextSequence, ActionType.LikeBack, ActionStatus.Pending, Maybe<ReplyText>.None, now)));
    }

    public UnitResult<Error> CompleteLikeBack(DateTimeOffset now) =>
        PendingOf(ActionType.LikeBack)
            .ToResult(MatchErrors.NoPendingAction(ActionType.LikeBack))
            .Bind(action => TransitionTo(MatchStatus.LikedBack).Tap(() =>
            {
                action.Complete(now);
                Raise(new LikeBackSent(Id, Profile, IsMutual, now));
            }));

    /// <summary>Confirm mode: keep the message as a draft until the owner decides where and whether to send it.</summary>
    /// <param name="anchor">The bot notification the draft answers; needed only for the bot-chat channel.</param>
    public UnitResult<Error> DraftReply(ReplyText text, Maybe<BotMessageId> anchor, DateTimeOffset now) =>
        Guard.Require(!HasActiveMessage, MatchErrors.AlreadyMessaged)
            .Bind(() => Guard.Require(CanStartMessage, MatchErrors.InvalidState(Status, "черновик сообщения")))
            .Bind(() => Guard.Require(PendingAction.HasNoValue, MatchErrors.ActionInProgress))
            .Tap(() =>
            {
                _actions.Add(MatchAction.Start(NextSequence, ActionType.SendMessage, ActionStatus.Draft, text, now, anchor));
                Raise(new ReplyDrafted(Id, Kind, Profile, text, IsMutual, anchor, now));
            });

    /// <summary>Marks the message as being sent through <paramref name="channel"/>. A draft, if any, becomes the message.</summary>
    public UnitResult<Error> StartMessage(ReplyText text, MessageChannel channel, DateTimeOffset now)
    {
        if (DraftMessage.TryGetValue(out var draft))
        {
            return Guard.Require(!IsClosed, MatchErrors.InvalidState(Status, "отправка черновика"))
                .Bind(() => Guard.Require(PendingAction.HasNoValue, MatchErrors.ActionInProgress))
                .Tap(() => draft.Promote(text, channel, now));
        }

        return Guard.Require(!HasActiveMessage, MatchErrors.AlreadyMessaged)
            .Bind(() => Guard.Require(CanStartMessage, MatchErrors.InvalidState(Status, "сообщение")))
            .Bind(() => Guard.Require(PendingAction.HasNoValue, MatchErrors.ActionInProgress))
            .Tap(() => _actions.Add(MatchAction.Start(
                NextSequence, ActionType.SendMessage, ActionStatus.Pending, text, now, channel: channel)));
    }

    public UnitResult<Error> CompleteMessage(DateTimeOffset now) =>
        PendingOf(ActionType.SendMessage)
            .ToResult(MatchErrors.NoPendingAction(ActionType.SendMessage))
            .Bind(action => TransitionTo(MatchStatus.MessageSent).Tap(() =>
            {
                action.Complete(now);
                Raise(new MessageSent(
                    Id, Kind, Profile, action.Text.GetValueOrThrow(), action.Channel.GetValueOrDefault(MessageChannel.BotChat), HasLikedBack, now));
            }));

    public UnitResult<Error> Close() => TransitionTo(MatchStatus.Done);

    public UnitResult<Error> SkipReply(DateTimeOffset now) =>
        DraftMessage
            .ToResult(MatchErrors.NoDraft)
            .Bind(draft => TransitionTo(MatchStatus.Skipped).Tap(() => draft.Skip(now)));

    /// <summary>
    /// The pending action did not happen (refused, rate limit, pause): release it so it can be started again later;
    /// an approved draft goes back to Draft. Status does not change.
    /// </summary>
    public UnitResult<Error> AbortPendingAction(Error reason, DateTimeOffset now)
    {
        if (PendingAction.TryGetValue(out var action))
        {
            action.Abort(reason.ToString(), now);
        }

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> Fail(Error reason, DateTimeOffset now) =>
        TransitionTo(MatchStatus.Failed).Tap(() =>
        {
            if (PendingAction.TryGetValue(out var action))
            {
                action.Fail(reason.ToString(), now);
            }

            Raise(new ProcessingFailed(Id, Profile, reason, now));
        });

    private UnitResult<Error> TransitionTo(MatchStatus next) =>
        Guard.Require(AllowedTransitions[Status].Contains(next), MatchErrors.InvalidTransition(Status, next))
            .Tap(() => Status = next);

    private Maybe<MatchAction> PendingOf(ActionType type) =>
        _actions.TryFirst(a => a.Type == type && a.Status == ActionStatus.Pending);
}
