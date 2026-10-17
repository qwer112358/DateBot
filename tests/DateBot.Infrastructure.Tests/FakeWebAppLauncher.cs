using CSharpFunctionalExtensions;
using DateBot.Domain.Common;
using DateBot.Infrastructure.Telegram;

namespace DateBot.Infrastructure.Tests;

/// <summary>Hands out a new init data string on every sign-in: "init-1", "init-2", …</summary>
internal sealed class FakeWebAppLauncher : IWebAppLauncher
{
    public int Opened { get; private set; }

    public Task<Result<WebAppSession, Error>> OpenWebAppAsync(Maybe<Uri> url, CancellationToken ct)
    {
        Opened++;
        return Task.FromResult(Result.Success<WebAppSession, Error>(
            new WebAppSession(new Uri("https://dating.example.app/"), $"init-{Opened}", 42)));
    }
}
