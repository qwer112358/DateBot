using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Options;
using FluentValidation;

namespace DateBot.Application.Replies.Validators;

public sealed class ReplyOptionsValidator : AbstractValidator<ReplyOptions>
{
    public ReplyOptionsValidator()
    {
        RuleFor(o => o.Owner.Name).NotEmpty().WithMessage("Укажите ваше имя для промпта (Replies:Owner:Name)");
        RuleFor(o => o.PromptsFile).NotEmpty().WithMessage("Укажите файл промптов (Replies:PromptsFile)");
        RuleFor(o => o.DefaultLanguage).IsInEnum();
        RuleFor(o => o.MinLength).GreaterThan(0);
        RuleFor(o => o.MaxLength).GreaterThan(o => o.MinLength).LessThanOrEqualTo(1000);
        RuleFor(o => o.MaxEmoji).GreaterThanOrEqualTo(0);
        RuleFor(o => o.InvalidReplyPolicy).IsInEnum();
        RuleFor(o => o.Templates)
            .Must(t => Enum.GetNames<ReplyLanguage>().All(lang => t.TryGetValue(lang, out var list) && list.Length > 0))
            .WithMessage("Нужен хотя бы один шаблон на каждый язык: Russian, Uzbek, English (Replies:Templates)");
    }
}
