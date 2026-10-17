using DateBot.Domain.Matches.States;
using FluentValidation;

namespace DateBot.Domain.Matches.Validators;

public sealed class MatchInputValidator : AbstractValidator<MatchInput>
{
    public MatchInputValidator()
    {
        RuleFor(m => m.Kind).IsInEnum().WithErrorCode("match.kind");
        RuleFor(m => m.ReceivedAt).NotEqual(default(DateTimeOffset)).WithErrorCode("match.received_at");
    }
}
