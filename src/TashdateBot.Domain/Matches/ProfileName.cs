using CSharpFunctionalExtensions;
using FluentValidation;
using TashdateBot.Domain.Common;

namespace TashdateBot.Domain.Matches;

public sealed record ProfileName
{
    public const int MaxLength = 64;
    private static readonly ProfileNameValidator Validator = new();

    private ProfileName(string value) => Value = value;

    public string Value { get; }

    public static Result<ProfileName, Error> Create(string value) =>
        Validator.ValidateAndCreate(TextRules.NormalizeSpaces(value), "profile_name.invalid", v => new ProfileName(v));

    public override string ToString() => Value;
}

public sealed class ProfileNameValidator : AbstractValidator<string>
{
    public ProfileNameValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("profile_name.empty")
            .MaximumLength(ProfileName.MaxLength).WithErrorCode("profile_name.too_long")
            .Must(v => !v.Contains('\n')).WithErrorCode("profile_name.multiline")
            .OverridePropertyName(nameof(ProfileName));
}
