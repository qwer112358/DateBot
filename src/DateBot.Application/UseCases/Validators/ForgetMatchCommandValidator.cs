using DateBot.Application.UseCases.Commands;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class ForgetMatchCommandValidator : AbstractValidator<ForgetMatchCommand>
{
    public ForgetMatchCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}
