using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

public sealed record City
{
    public const int MaxLength = 64;
    private static readonly CityValidator Validator = new();

    private City(string value) => Value = value;

    public string Value { get; }

    public static Result<City, Error> Create(string value) =>
        Validator.ValidateAndCreate(TextRules.NormalizeSpaces(value), "city.invalid", v => new City(v));

    public override string ToString() => Value;
}
