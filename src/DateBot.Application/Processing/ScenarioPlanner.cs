using CSharpFunctionalExtensions;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;

namespace DateBot.Application.Processing;

public abstract record ScenarioPlan;

public sealed record IgnorePlan(string Reason) : ScenarioPlan;

public sealed record ExecutePlan(bool CreateMatch, bool LikeBack, bool Message) : ScenarioPlan;

/// <summary>
/// Pure decision: what to do with a notification given what we already know about the person.
/// Resuming the same notification after a restart repeats only the unfinished steps.
/// </summary>
public static class ScenarioPlanner
{
    public static ScenarioPlan Plan(
        MatchKind notificationKind, Maybe<Match> existing, BotMessageId notificationId, LikedMeMessagePolicy policy)
    {
        if (existing.HasNoValue)
        {
            return notificationKind == MatchKind.LikedMe
                ? new ExecutePlan(CreateMatch: true, LikeBack: true, Message: policy == LikedMeMessagePolicy.AfterLikeBack)
                : new ExecutePlan(CreateMatch: true, LikeBack: false, Message: true);
        }

        var match = existing.GetValueOrThrow();

        if (match.IsClosed)
        {
            return new IgnorePlan($"Анкета {match.Profile.Name} уже обработана (статус {match.Status})");
        }

        if (match.SourceMessageId == notificationId)
        {
            return Resume(match, policy);
        }

        // Deferred scenario A: we liked back earlier, now the mutual like makes the message possible.
        if (notificationKind == MatchKind.Mutual && match.CanStartMessage)
        {
            return new ExecutePlan(CreateMatch: false, LikeBack: false, Message: true);
        }

        return new IgnorePlan($"Повторное уведомление о {match.Profile.Name}: автосообщение уже было");
    }

    private static ExecutePlan Resume(Match match, LikedMeMessagePolicy policy)
    {
        var pendingLike = match.PendingAction.HasValue && match.PendingAction.GetValueOrThrow().Type == ActionType.LikeBack;
        var pendingMessage = match.PendingAction.HasValue && match.PendingAction.GetValueOrThrow().Type == ActionType.SendMessage;
        var messageAllowedNow = match.Kind == MatchKind.Mutual || policy == LikedMeMessagePolicy.AfterLikeBack;

        return new ExecutePlan(
            CreateMatch: false,
            LikeBack: match.Kind == MatchKind.LikedMe && (match.CanLikeBack || pendingLike),
            Message: pendingMessage || (messageAllowedNow && !match.HasActiveMessage));
    }
}
