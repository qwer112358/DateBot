using FluentValidation;

namespace DateBot.Domain.Inbox;

public sealed class InboxMessageStateValidator : AbstractValidator<InboxMessageState>
{
    public InboxMessageStateValidator()
    {
        RuleFor(m => m.RawText).MaximumLength(InboxMessage.MaxRawTextLength).WithErrorCode("inbox_message.too_long");
        RuleFor(m => m.Status).IsInEnum().WithErrorCode("inbox_message.status");
    }
}
