using DateBot.Application.UseCases.Commands;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class LookUpContactsCommandValidator : AbstractValidator<LookUpContactsCommand>
{
    public LookUpContactsCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}
