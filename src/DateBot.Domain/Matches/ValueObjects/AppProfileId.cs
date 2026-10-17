using System.Globalization;
using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

/// <summary>Id of the person's profile inside the dating app (Mini App); needed to like back and to use the in-app chat.</summary>
public sealed record AppProfileId
{
    private static readonly AppProfileIdValidator Validator = new();

    private AppProfileId(long value) => Value = value;

    public long Value { get; }

    public static Result<AppProfileId, Error> Create(long value) =>
        Validator.ValidateAndCreate(value, "app_profile_id.invalid", v => new AppProfileId(v));

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
