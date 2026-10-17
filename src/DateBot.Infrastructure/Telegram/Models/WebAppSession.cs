namespace DateBot.Infrastructure.Telegram.Models;

/// <summary>The bot's Mini App opened as your account.</summary>
/// <param name="BaseAddress">Origin of the Mini App; its API lives under <c>/api/</c>.</param>
/// <param name="InitData">Signed <c>Telegram.WebApp.initData</c> the app checks on every request.</param>
/// <param name="TelegramUserId">Your Telegram user id, as in the init data.</param>
internal sealed record WebAppSession(Uri BaseAddress, string InitData, long TelegramUserId);
