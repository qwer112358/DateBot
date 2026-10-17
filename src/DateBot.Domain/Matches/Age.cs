using System.Globalization;
using CSharpFunctionalExtensions;
using FluentValidation;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record Age
{
    public const int Min = 18;
    public const int Max = 99;
    private static readonly AgeValidator Validator = new();

    private Age(int value) => Value = value;

    public int Value { get; }

    public static Result<Age, Error> Create(int value) =>
        Validator.ValidateAndCreate(value, "age.invalid", v => new Age(v));

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed class AgeValidator : AbstractValidator<int>
{
    public AgeValidator() =>
        RuleFor(v => v)
            .InclusiveBetween(Age.Min, Age.Max).WithErrorCode("age.out_of_range")
            .OverridePropertyName(nameof(Age));
}
