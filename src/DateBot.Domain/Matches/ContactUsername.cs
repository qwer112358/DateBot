using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Domain.Matches;

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
