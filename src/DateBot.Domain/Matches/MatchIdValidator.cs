using FluentValidation;

namespace DateBot.Domain.Matches;

public sealed class MatchIdValidator : AbstractValidator<Guid>
{
    public MatchIdValidator() =>
        RuleFor(v => v).NotEmpty().OverridePropertyName(nameof(MatchId)).WithErrorCode("match_id.empty");
}
