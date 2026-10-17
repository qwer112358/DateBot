using DateBot.Infrastructure.Notifier.Options;
using FluentValidation;

namespace DateBot.Infrastructure.Notifier.Validators;

public sealed class NotifierOptionsValidator : AbstractValidator<NotifierOptions>
{
    public NotifierOptionsValidator()
    {
        RuleFor(o => o.BotToken).Matches(@"^(\d+:[\w-]{20,})?$").WithMessage("Notifier:BotToken не похож на токен BotFather");
        RuleFor(o => o.Verbosity).IsInEnum();
        RuleFor(o => o.QuietFrom).InclusiveBetween(TimeSpan.Zero, TimeSpan.FromHours(24));
        RuleFor(o => o.QuietTo).InclusiveBetween(TimeSpan.Zero, TimeSpan.FromHours(24));
        RuleFor(o => o.TimeZone)
            .Must(id => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _))
            .WithMessage(o => $"Неизвестный часовой пояс {o.TimeZone}");
    }
}
