using FluentValidation;

namespace DateBot.Application.UseCases;

public sealed class LookUpContactsCommandValidator : AbstractValidator<LookUpContactsCommand>
{
    public LookUpContactsCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}
