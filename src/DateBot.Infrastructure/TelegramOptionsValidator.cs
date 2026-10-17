using FluentValidation;

namespace DateBot.Infrastructure;

public sealed class TelegramOptionsValidator : AbstractValidator<TelegramOptions>
{
    public TelegramOptionsValidator()
    {
        RuleFor(o => o.ApiId).GreaterThan(0).WithMessage("Задайте Telegram:ApiId (my.telegram.org) в user-secrets");
        RuleFor(o => o.ApiHash).Matches("^[0-9a-fA-F]{32}$").WithMessage("Задайте Telegram:ApiHash (32 hex-символа) в user-secrets");
        RuleFor(o => o.PhoneNumber).Matches(@"^\+\d{7,15}$").WithMessage("Telegram:PhoneNumber в формате +998901234567");
        RuleFor(o => o.TashdateBotUsername).Matches("^@?[A-Za-z][A-Za-z0-9_]{3,31}$").WithMessage("Укажите имя бота Tashdate (Telegram:TashdateBotUsername)");
        RuleFor(o => o.SessionFile).NotEmpty();
    }
}
