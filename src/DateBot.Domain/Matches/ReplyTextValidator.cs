using DateBot.Domain.Common;
using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class ReplyTextValidator : AbstractValidator<string>
{
    public ReplyTextValidator() =>
        RuleFor(v => v)
            .Length(ReplyText.MinLength, ReplyText.MaxLength).WithErrorCode("reply_text.length")
            .Must(v => !TextRules.ContainsLink(v)).WithErrorCode("reply_text.link").WithMessage("Текст содержит ссылку")
            .Must(v => !TextRules.ContainsPhone(v)).WithErrorCode("reply_text.phone").WithMessage("Текст содержит номер телефона")
            .Must(v => !TextRules.ContainsMention(v)).WithErrorCode("reply_text.mention").WithMessage("Текст содержит @упоминание")
            .OverridePropertyName(nameof(ReplyText));
}
