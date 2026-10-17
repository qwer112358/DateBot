namespace DateBot.Infrastructure.Platform;

internal sealed class ConsoleLoginPrompt : ILoginPrompt
{
    public string Ask(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public string AskSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var secret = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return secret.ToString();
            }

            if (key.Key == ConsoleKey.Backspace && secret.Length > 0)
            {
                secret.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
            }
        }
    }
}
