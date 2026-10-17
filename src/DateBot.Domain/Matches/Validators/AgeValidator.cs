using DateBot.Domain.Matches.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class AgeValidator : AbstractValidator<int>
{
    public AgeValidator() =>
        RuleFor(v => v)
            .InclusiveBetween(Age.Min, Age.Max).WithErrorCode("age.out_of_range")
            .OverridePropertyName(nameof(Age));
}
