using CSharpFunctionalExtensions;
using FluentValidation;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

/// <summary>Free text written by a stranger. Treated as data, never as instructions.</summary>
public sealed record Bio
{
    public const int MaxLength = 4000;
    private static readonly BioValidator Validator = new();

    private Bio(string value) => Value = value;

    public string Value { get; }

    public static Result<Bio, Error> Create(string value) =>
        Validator.ValidateAndCreate(value.Trim(), "bio.invalid", v => new Bio(v));

    public override string ToString() => Value;
}

public sealed class BioValidator : AbstractValidator<string>
{
    public BioValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("bio.empty")
            .MaximumLength(Bio.MaxLength).WithErrorCode("bio.too_long")
            .OverridePropertyName(nameof(Bio));
}
