using FluentValidation;

namespace DateBot.Application.UseCases;

public sealed class ProcessAppLikeCommandValidator : AbstractValidator<ProcessAppLikeCommand>
{
    public ProcessAppLikeCommandValidator() => RuleFor(c => c.Like.ProfileId).GreaterThan(0);
}
