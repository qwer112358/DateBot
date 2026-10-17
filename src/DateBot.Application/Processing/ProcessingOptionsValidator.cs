using FluentValidation;

namespace DateBot.Application.Processing;

public sealed class ProcessingOptionsValidator : AbstractValidator<ProcessingOptions>
{
    public ProcessingOptionsValidator()
    {
        RuleFor(o => o.Mode).IsInEnum();
        RuleFor(o => o.LikedMeMessage).IsInEnum();
        RuleFor(o => o.MutualRoute).IsInEnum();
        RuleFor(o => o.LikeBackButtons).NotEmpty().WithMessage("Укажите подписи кнопки лайка в ответ (Processing:LikeBackButtons)");
        RuleFor(o => o.MinPause).GreaterThanOrEqualTo(TimeSpan.Zero);
        RuleFor(o => o.MaxPause).GreaterThanOrEqualTo(o => o.MinPause).WithMessage("MaxPause должна быть не меньше MinPause");
        RuleFor(o => o.ButtonToTextPause).GreaterThanOrEqualTo(TimeSpan.Zero);
        RuleFor(o => o.MaxActionsPerHour).GreaterThan(0);
        RuleFor(o => o.HistoryBatchSize).InclusiveBetween(1, 100);
        RuleFor(o => o.PollInterval).GreaterThan(TimeSpan.Zero);
        RuleFor(o => o.RetentionDays).GreaterThanOrEqualTo(0);
        RuleFor(o => o.FirstRunBackfill).InclusiveBetween(TimeSpan.Zero, TimeSpan.FromDays(30));
        RuleFor(o => o.Filters.MinAge).InclusiveBetween(18, 99);
        RuleFor(o => o.Filters.MaxAge).GreaterThanOrEqualTo(o => o.Filters.MinAge);
    }
}
