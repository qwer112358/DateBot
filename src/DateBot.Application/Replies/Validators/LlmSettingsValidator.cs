using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Models;
using FluentValidation;

namespace DateBot.Application.Replies.Validators;

public sealed class LlmSettingsValidator : AbstractValidator<LlmSettings>
{
    public LlmSettingsValidator()
    {
        RuleFor(s => s.Model).NotEmpty().WithMessage("Model — имя модели, например claude-haiku-5-5");
        RuleFor(s => s.Effort).IsInEnum().WithMessage("Effort — Low, Medium, High, Xhigh или Max");
        RuleFor(s => s.MaxTokens).InclusiveBetween(256, 16000).WithMessage("MaxTokens — от 256 до 16000");
        RuleFor(s => s.MaxPhotos).InclusiveBetween(0, 10).WithMessage("MaxPhotos — от 0 до 10");
        RuleFor(s => s.MinLength).GreaterThan(0).WithMessage("MinLength — больше 0");
        RuleFor(s => s.MaxLength).GreaterThan(s => s.MinLength).LessThanOrEqualTo(1000).WithMessage("MaxLength — больше MinLength и не больше 1000");
        RuleFor(s => s.MaxEmoji).GreaterThanOrEqualTo(0).WithMessage("MaxEmoji — 0 или больше");
        RuleFor(s => s.DefaultLanguage).IsInEnum().WithMessage("DefaultLanguage — Russian, Uzbek или English");
        RuleFor(s => s.InvalidReplyPolicy).IsInEnum().WithMessage("InvalidReplyPolicy — Template или Confirm");
        RuleFor(s => s.Templates)
            .Must(t => Enum.GetNames<ReplyLanguage>().All(lang => t.TryGetValue(lang, out var list) && list.Length > 0))
            .WithMessage("Templates — хотя бы один шаблон на каждый язык: Russian, Uzbek, English");
    }
}
