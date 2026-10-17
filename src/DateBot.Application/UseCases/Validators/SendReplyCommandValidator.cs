using DateBot.Application.UseCases.Commands;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class SendReplyCommandValidator : AbstractValidator<SendReplyCommand>
{
    public SendReplyCommandValidator()
    {
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.Channel).IsInEnum();
    }
}
