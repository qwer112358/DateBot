using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.States;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class MatchStateValidator : AbstractValidator<MatchState>
{
    public MatchStateValidator()
    {
        RuleFor(m => m.Kind).IsInEnum().WithErrorCode("match.kind");
        RuleFor(m => m.Status).IsInEnum().WithErrorCode("match.status");
        RuleForEach(m => m.Actions).ChildRules(action =>
        {
            action.RuleFor(a => a.Type).IsInEnum().WithErrorCode("match.action_type");
            action.RuleFor(a => a.Status).IsInEnum().WithErrorCode("match.action_status");
            action.RuleFor(a => a.Text.HasValue)
                .Equal(true)
                .When(a => a.Type == ActionType.SendMessage)
                .WithErrorCode("match.message_without_text");
            action.RuleFor(a => a.Channel.GetValueOrDefault(MessageChannel.BotChat)).IsInEnum().WithErrorCode("match.action_channel");
        });
        RuleFor(m => m.Actions)
            .Must(a => a.Select(x => x.Sequence).Distinct().Count() == a.Count)
            .WithErrorCode("match.duplicate_action_sequence")
            .Must(a => a.Count(x => x.Status == ActionStatus.Pending) <= 1)
            .WithErrorCode("match.many_pending_actions")
            .Must(a => a.Count(x => x.Type == ActionType.SendMessage
                && x.Status is ActionStatus.Draft or ActionStatus.Pending or ActionStatus.Done) <= 1)
            .WithErrorCode("match.many_messages")
            .Must(a => a.Count(x => x.Type == ActionType.LikeBack
                && x.Status is ActionStatus.Draft or ActionStatus.Pending or ActionStatus.Done) <= 1)
            .WithErrorCode("match.many_like_backs");
        RuleFor(m => m)
            .Must(m => m.Kind == MatchKind.LikedMe || m.Actions.All(a => a.Type != ActionType.LikeBack))
            .WithErrorCode("match.like_back_on_mutual");
    }
}
