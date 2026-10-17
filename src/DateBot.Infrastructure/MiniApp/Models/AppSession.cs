using DateBot.Infrastructure.Telegram.Models;

namespace DateBot.Infrastructure.MiniApp.Models;

/// <summary>A signed-in session with the app: the Telegram init data, your app profile id and the app's labels.</summary>
internal sealed record AppSession(WebAppSession WebApp, long OwnId, AppTranslations Translations, DateTimeOffset OpenedAt);
