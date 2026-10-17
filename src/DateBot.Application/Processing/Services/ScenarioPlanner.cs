using CSharpFunctionalExtensions;
using DateBot.Application.Processing.Plans;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Shared.ValueObjects;

namespace DateBot.Application.Processing.Services;

/// <summary>
/// Pure decision: what to do with a notification given what we already know about the person.
/// Resuming the same notification after a restart repeats only the unfinished steps. Whether the message can go
/// right now (a match is needed for most channels) is decided by the message step itself.
/// </summary>
public static class ScenarioPlanner
{
    /// <param name="source">The bot notification; none for a like found in the app.</param>
    public static ScenarioPlan Plan(MatchKind notificationKind, Maybe<BotMessageId> source, Maybe<Match> existing)
    {
        if (existing.HasNoValue)
        {
            return new ExecutePlan(CreateMatch: true, LikeBack: notificationKind == MatchKind.LikedMe, Message: true);
        }

        var match = existing.GetValueOrThrow();

        if (match.IsClosed)
        {
            return new IgnorePlan($"Анкета {match.Profile.Name} уже обработана (статус {match.Status})");
        }

        if (match.SourceMessageId.Equals(source))
        {
            return Resume(match);
        }

        // Deferred scenario A: we liked back earlier, now the mutual like makes the message possible.
        if (notificationKind == MatchKind.Mutual && match.CanStartMessage)
        {
            return new ExecutePlan(CreateMatch: false, LikeBack: false, Message: true);
        }

        return new IgnorePlan($"Повторное уведомление о {match.Profile.Name}: автосообщение уже было");
    }

    private static ExecutePlan Resume(Match match)
    {
        var pendingLike = match.PendingAction.HasValue && match.PendingAction.GetValueOrThrow().Type == ActionType.LikeBack;
        var pendingMessage = match.PendingAction.HasValue && match.PendingAction.GetValueOrThrow().Type == ActionType.SendMessage;

        return new ExecutePlan(
            CreateMatch: false,
            LikeBack: match.Kind == MatchKind.LikedMe && (match.CanLikeBack || pendingLike),
            Message: pendingMessage || !match.HasActiveMessage);
    }
}
