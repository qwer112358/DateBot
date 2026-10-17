using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class ProfileInputValidator : AbstractValidator<ProfileInput>
{
    public ProfileInputValidator() =>
        RuleFor(p => p.RawText)
            .MaximumLength(Profile.MaxRawTextLength).WithErrorCode("profile.raw_text_too_long");
}
