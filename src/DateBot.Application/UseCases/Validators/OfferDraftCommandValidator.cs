using DateBot.Application.UseCases.Commands;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class OfferDraftCommandValidator : AbstractValidator<OfferDraftCommand>
{
    public OfferDraftCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}
