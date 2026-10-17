using FluentValidation;

namespace DateBot.Infrastructure.MiniApp;

public sealed class MiniAppClientOptionsValidator : AbstractValidator<MiniAppClientOptions>
{
    public MiniAppClientOptionsValidator()
    {
        RuleFor(o => o.Url)
            .Must(url => url.Length == 0 || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("MiniAppClient:Url — пусто или https-адрес Mini App");
        RuleFor(o => o.Language).Matches("^[a-z]{2}$").WithMessage("MiniAppClient:Language — код языка, например ru или uz");
        RuleFor(o => o.Timeout).InclusiveBetween(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(2));
        RuleFor(o => o.SessionLifetime).InclusiveBetween(TimeSpan.FromMinutes(1), TimeSpan.FromHours(12));
    }
}
