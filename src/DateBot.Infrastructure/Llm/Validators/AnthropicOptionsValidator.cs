using DateBot.Infrastructure.Llm.Options;
using FluentValidation;

namespace DateBot.Infrastructure.Llm.Validators;

public sealed class AnthropicOptionsValidator : AbstractValidator<AnthropicOptions>
{
    public AnthropicOptionsValidator()
    {
        RuleFor(o => o.Model).NotEmpty();
        RuleFor(o => o.Effort).IsInEnum();
        RuleFor(o => o.MaxTokens).InclusiveBetween(256, 16000);
        RuleFor(o => o.Timeout).GreaterThan(TimeSpan.Zero);
    }
}
