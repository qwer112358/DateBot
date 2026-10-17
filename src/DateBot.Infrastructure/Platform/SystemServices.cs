using DateBot.Application.Ports;

namespace DateBot.Infrastructure.Platform;

internal sealed class SystemClock(TimeProvider time) : IClock
{
    public DateTimeOffset UtcNow => time.GetUtcNow();

    public Task DelayAsync(TimeSpan delay, CancellationToken ct) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, time, ct);
}

internal sealed class RandomJitter : IJitter
{
    public TimeSpan Next(TimeSpan min, TimeSpan max) =>
        max <= min ? min : min + TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * (max - min).TotalMilliseconds);

    public int NextIndex(int count) => count <= 1 ? 0 : Random.Shared.Next(count);
}

/// <summary>Asks for the Telegram login code (and 2FA password) on the first run.</summary>
public interface ILoginPrompt
{
    string Ask(string prompt);

    string AskSecret(string prompt);
}

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
