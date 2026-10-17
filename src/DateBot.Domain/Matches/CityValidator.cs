using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class CityValidator : AbstractValidator<string>
{
    public CityValidator() =>
        RuleFor(v => v)
            .NotEmpty().WithErrorCode("city.empty")
            .MaximumLength(City.MaxLength).WithErrorCode("city.too_long")
            .OverridePropertyName(nameof(City));
}
