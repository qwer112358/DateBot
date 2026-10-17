using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class BioValidator : AbstractValidator<string>
{
    public BioValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("bio.empty")
            .MaximumLength(Bio.MaxLength).WithErrorCode("bio.too_long")
            .OverridePropertyName(nameof(Bio));
}
