using DateBot.Domain.Shared.ValueObjects;
using FluentValidation;

namespace DateBot.Domain.Shared.Validators;

public sealed class BotMessageIdValidator : AbstractValidator<long>
{
    public BotMessageIdValidator() =>
        RuleFor(v => v).GreaterThan(0).OverridePropertyName(nameof(BotMessageId)).WithErrorCode("bot_message_id.not_positive");
}
