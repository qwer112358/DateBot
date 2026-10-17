using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class ContactUsernameValidator : AbstractValidator<string>
{
    public ContactUsernameValidator() =>
        RuleFor(v => v)
            .Matches("^[A-Za-z][A-Za-z0-9_]{4,31}$").WithErrorCode("contact_username.format")
            .OverridePropertyName(nameof(ContactUsername));
}
