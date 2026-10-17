using System.Globalization;
using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

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
