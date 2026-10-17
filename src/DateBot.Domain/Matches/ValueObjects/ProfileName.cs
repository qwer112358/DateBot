using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

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
