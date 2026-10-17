using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

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
