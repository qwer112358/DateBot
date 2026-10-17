using System.Collections.Immutable;
using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class InterestsValidator : AbstractValidator<ImmutableArray<string>>
{
    public InterestsValidator()
    {
        RuleFor(v => v.Length)
            .LessThanOrEqualTo(Interests.MaxCount).WithErrorCode("interests.too_many")
            .OverridePropertyName(nameof(Interests));
        RuleForEach(v => v)
            .MaximumLength(Interests.MaxItemLength).WithErrorCode("interests.item_too_long")
            .OverridePropertyName(nameof(Interests));
    }
}
