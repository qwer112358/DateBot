using DateBot.Infrastructure.Llm.Options;
using FluentValidation;

namespace DateBot.Infrastructure.Llm.Validators;

public sealed class AnthropicOptionsValidator : AbstractValidator<AnthropicOptions>
{
    public AnthropicOptionsValidator()
    {
        RuleFor(o => o.Timeout).GreaterThan(TimeSpan.Zero);
    }
}
