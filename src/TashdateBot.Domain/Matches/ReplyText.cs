using CSharpFunctionalExtensions;
using FluentValidation;
using TashdateBot.Domain.Common;

namespace TashdateBot.Domain.Matches;

/// <summary>The auto-message text. Invariant: bounded length, no links, phones or @mentions.</summary>
public sealed record ReplyText
{
    public const int MinLength = 2;
    public const int MaxLength = 1000;
    private static readonly ReplyTextValidator Validator = new();

    private ReplyText(string value) => Value = value;

    public string Value { get; }

    public static Result<ReplyText, Error> Create(string value) =>
        Validator.ValidateAndCreate(value.Trim(), "reply_text.invalid", v => new ReplyText(v));

    public override string ToString() => Value;
}

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
