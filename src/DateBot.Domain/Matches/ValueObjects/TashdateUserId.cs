using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

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
