using CSharpFunctionalExtensions;
using FluentValidation;
using TashdateBot.Domain.Common;

namespace TashdateBot.Domain.Matches;

/// <summary>Telegram username the bot reveals on a mutual like, stored without the at-sign.</summary>
public sealed record ContactUsername
{
    private static readonly ContactUsernameValidator Validator = new();

    private ContactUsername(string value) => Value = value;

    public string Value { get; }

    public static Result<ContactUsername, Error> Create(string value) =>
        Validator.ValidateAndCreate(value.Trim().TrimStart('@'), "contact_username.invalid", v => new ContactUsername(v));

    public override string ToString() => "@" + Value;
}

public sealed class ContactUsernameValidator : AbstractValidator<string>
{
    public ContactUsernameValidator() =>
        RuleFor(v => v)
            .Matches("^[A-Za-z][A-Za-z0-9_]{4,31}$").WithErrorCode("contact_username.format")
            .OverridePropertyName(nameof(ContactUsername));
}
