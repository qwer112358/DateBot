using FluentValidation;

namespace DateBot.Application.UseCases;

public sealed class ForgetMatchCommandValidator : AbstractValidator<ForgetMatchCommand>
{
    public ForgetMatchCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}
