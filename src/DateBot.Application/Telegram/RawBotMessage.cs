using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace DateBot.Application.Telegram;

/// <summary>An incoming message from the Tashdate bot, already stripped of Telegram types.</summary>
public sealed record RawBotMessage(
    long Id,
    DateTimeOffset SentAt,
    string Text,
    ImmutableArray<BotButton> Buttons,
    Maybe<string> PhotoId)
{
    /// <summary>Text kept in the inbox: message text plus the list of buttons, for manual review.</summary>
    public string ToArchiveText()
    {
        var buttons = Buttons.IsDefaultOrEmpty ? string.Empty : $"\n\n[Кнопки: {string.Join(" | ", Buttons.Select(b => b.Text))}]";
        var photo = PhotoId.HasValue ? "\n[Фото]" : string.Empty;
        return Text + buttons + photo;
    }
}
