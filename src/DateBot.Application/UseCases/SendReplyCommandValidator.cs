using FluentValidation;

namespace DateBot.Application.UseCases;

public sealed class SendReplyCommandValidator : AbstractValidator<SendReplyCommand>
{
    public SendReplyCommandValidator() => RuleFor(c => c.MatchId).NotEmpty();
}
