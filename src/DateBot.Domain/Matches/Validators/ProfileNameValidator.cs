using DateBot.Domain.Matches.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class ProfileNameValidator : AbstractValidator<string>
{
    public ProfileNameValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("profile_name.empty")
            .MaximumLength(ProfileName.MaxLength).WithErrorCode("profile_name.too_long")
            .Must(v => !v.Contains('\n')).WithErrorCode("profile_name.multiline")
            .OverridePropertyName(nameof(ProfileName));
}
