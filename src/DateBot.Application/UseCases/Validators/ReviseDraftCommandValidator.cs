using DateBot.Application.UseCases.Commands;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class ReviseDraftCommandValidator : AbstractValidator<ReviseDraftCommand>
{
    public ReviseDraftCommandValidator()
    {
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.Input).NotEmpty().WithMessage("Пустое сообщение");
    }
}
