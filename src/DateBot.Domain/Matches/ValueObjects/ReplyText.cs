using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

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
