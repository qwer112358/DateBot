using CSharpFunctionalExtensions;
using DateBot.Domain.Common;

namespace DateBot.Infrastructure.Telegram;

/// <summary>Opens the dating bot's Mini App the way the "Открыть знакомства" button does, without a browser.</summary>
internal interface IWebAppLauncher
{
    /// <param name="url">Mini App address from the config; none = the bot's menu button or web-app button.</param>
    Task<Result<WebAppSession, Error>> OpenWebAppAsync(Maybe<Uri> url, CancellationToken ct);
}
