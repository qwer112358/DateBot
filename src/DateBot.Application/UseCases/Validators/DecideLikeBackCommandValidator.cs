using DateBot.Application.UseCases.Commands;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class DecideLikeBackCommandValidator : AbstractValidator<DecideLikeBackCommand>
{
    public DecideLikeBackCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}
