using DateBot.Domain.Matches.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class AppProfileIdValidator : AbstractValidator<long>
{
    public AppProfileIdValidator() =>
        RuleFor(v => v).GreaterThan(0).OverridePropertyName(nameof(AppProfileId)).WithErrorCode("app_profile_id.not_positive");
}
