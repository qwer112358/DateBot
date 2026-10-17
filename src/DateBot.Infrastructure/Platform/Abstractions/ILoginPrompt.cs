namespace DateBot.Infrastructure.Platform.Abstractions;

/// <summary>Asks for the Telegram login code (and 2FA password) on the first run.</summary>
public interface ILoginPrompt
{
    string Ask(string prompt);

    string AskSecret(string prompt);
}
