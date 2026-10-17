using System.Collections.Immutable;
using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class ProfileFactsValidator : AbstractValidator<ImmutableArray<string>>
{
    public ProfileFactsValidator()
    {
        RuleFor(v => v.Length)
            .LessThanOrEqualTo(ProfileFacts.MaxCount).WithErrorCode("profile_facts.too_many")
            .OverridePropertyName(nameof(ProfileFacts));
        RuleForEach(v => v)
            .MaximumLength(ProfileFacts.MaxItemLength).WithErrorCode("profile_facts.item_too_long")
            .Must(v => !v.Contains('\n')).WithErrorCode("profile_facts.multiline")
            .OverridePropertyName(nameof(ProfileFacts));
    }
}
