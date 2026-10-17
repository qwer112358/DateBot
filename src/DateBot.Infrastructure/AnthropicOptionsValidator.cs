using FluentValidation;

namespace DateBot.Infrastructure;

public sealed class AnthropicOptionsValidator : AbstractValidator<AnthropicOptions>
{
    public AnthropicOptionsValidator()
    {
        RuleFor(o => o.Model).NotEmpty();
        RuleFor(o => o.MaxTokens).InclusiveBetween(256, 16000);
        RuleFor(o => o.Timeout).GreaterThan(TimeSpan.Zero);
    }
}
