using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class AppProfileIdValidator : AbstractValidator<long>
{
    public AppProfileIdValidator() =>
        RuleFor(v => v).GreaterThan(0).OverridePropertyName(nameof(AppProfileId)).WithErrorCode("app_profile_id.not_positive");
}
