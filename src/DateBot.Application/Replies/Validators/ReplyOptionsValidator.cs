using DateBot.Application.Replies.Options;
using FluentValidation;

namespace DateBot.Application.Replies.Validators;

public sealed class ReplyOptionsValidator : AbstractValidator<ReplyOptions>
{
    public ReplyOptionsValidator()
    {
        RuleFor(o => o.Owner.Name).NotEmpty().WithMessage("Укажите ваше имя для промпта (Replies:Owner:Name)");
        RuleFor(o => o.PromptsFile).NotEmpty().WithMessage("Укажите файл промптов (Replies:PromptsFile)");
        RuleFor(o => o.LlmFile).NotEmpty().WithMessage("Укажите файл настроек модели (Replies:LlmFile)");
    }
}
