using DateBot.Application.UseCases.Commands;
using DateBot.Domain.Inbox;
using FluentValidation;

namespace DateBot.Application.UseCases.Validators;

public sealed class ProcessBotMessageCommandValidator : AbstractValidator<ProcessBotMessageCommand>
{
    public ProcessBotMessageCommandValidator()
    {
        RuleFor(c => c.Message.Id).GreaterThan(0);
        RuleFor(c => c.Message.Text).NotNull().MaximumLength(InboxMessage.MaxRawTextLength - 1000);
        RuleFor(c => c.Message.SentAt).NotEqual(default(DateTimeOffset));
    }
}
