using DateBot.Domain.Matches.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class TashdateUserIdValidator : AbstractValidator<string>
{
    public TashdateUserIdValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("tashdate_user_id.empty")
            .MaximumLength(TashdateUserId.MaxLength).WithErrorCode("tashdate_user_id.too_long")
            .Matches(@"^\S+$").WithErrorCode("tashdate_user_id.whitespace")
            .OverridePropertyName(nameof(TashdateUserId));
}
