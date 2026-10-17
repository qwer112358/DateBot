using DateBot.Application.UseCases.Commands;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class ProcessAppLikeCommandValidator : AbstractValidator<ProcessAppLikeCommand>
{
    public ProcessAppLikeCommandValidator() => RuleFor(c => c.Like.ProfileId).GreaterThan(0);
}
