using System.Globalization;
using System.Text;
using CSharpFunctionalExtensions;
using FluentValidation;
using DateBot.Domain.Common;

namespace DateBot.Application.Replies;

public sealed record ReplyCandidate(string Text, ReplyLanguage ExpectedLanguage, int MinLength, int MaxLength, int MaxEmoji);

/// <summary>Checks the LLM output before it can be sent.</summary>
public sealed class GeneratedReplyValidator : AbstractValidator<ReplyCandidate>
{
    private static readonly string[] InstructionLeaks =
    [
        "игнорир", "инструкц", "промпт", "prompt", "system", "ignore previous", "ignore all", "as an ai",
        "языковая модель", "language model", "я ии", "я — ии", "нейросет", "<profile", "</", "правила выше",
    ];

    public GeneratedReplyValidator()
    {
        RuleFor(c => c.Text).NotEmpty().WithErrorCode("reply.empty");
        RuleFor(c => c.Text.Length)
            .Must((c, length) => length >= c.MinLength && length <= c.MaxLength)
            .WithErrorCode("reply.length")
            .WithMessage(c => $"Длина {c.Text.Length} вне диапазона {c.MinLength}–{c.MaxLength}");
        RuleFor(c => c.Text)
            .Must(t => !TextRules.ContainsLink(t)).WithErrorCode("reply.link").WithMessage("Есть ссылка")
            .Must(t => !TextRules.ContainsPhone(t)).WithErrorCode("reply.phone").WithMessage("Есть номер телефона")
            .Must(t => !TextRules.ContainsMention(t)).WithErrorCode("reply.mention").WithMessage("Есть @упоминание")
            .Must(t => !InstructionLeaks.Any(m => t.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .WithErrorCode("reply.instructions").WithMessage("Похоже на текст инструкций или ответ на инъекцию");
        RuleFor(c => c)
            .Must(c => CountEmoji(c.Text) <= c.MaxEmoji)
            .WithErrorCode("reply.emoji").WithMessage("Слишком много эмодзи");
        RuleFor(c => c)
            .Must(c => LanguageDetector.Detect(c.Text).Map(l => l == c.ExpectedLanguage).GetValueOrDefault(true))
            .WithErrorCode("reply.language").WithMessage(c => $"Язык ответа не совпадает с ожидаемым ({c.ExpectedLanguage})");
    }

    public static int CountEmoji(string text) =>
        text.EnumerateRunes().Count(r =>
            Rune.GetUnicodeCategory(r) == UnicodeCategory.OtherSymbol && (r.Value >= 0x1F000 || r.Value is >= 0x2600 and <= 0x27BF));
}
