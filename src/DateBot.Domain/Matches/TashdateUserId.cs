using CSharpFunctionalExtensions;
using FluentValidation;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

/// <summary>
/// Stable identity of a person in Tashdate: an id the bot shows, a Telegram user id or username,
/// or a fingerprint of the profile when the bot shows neither.
/// </summary>
public sealed record TashdateUserId
{
    public const int MaxLength = 128;
    private static readonly TashdateUserIdValidator Validator = new();

    private TashdateUserId(string value) => Value = value;

    public string Value { get; }

    public static Result<TashdateUserId, Error> Create(string value) =>
        Validator.ValidateAndCreate(value.Trim(), "tashdate_user_id.invalid", v => new TashdateUserId(v));

    public override string ToString() => Value;
}

public sealed class TashdateUserIdValidator : AbstractValidator<string>
{
    public TashdateUserIdValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("tashdate_user_id.empty")
            .MaximumLength(TashdateUserId.MaxLength).WithErrorCode("tashdate_user_id.too_long")
            .Matches(@"^\S+$").WithErrorCode("tashdate_user_id.whitespace")
            .OverridePropertyName(nameof(TashdateUserId));
}
