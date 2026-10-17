using DateBot.Application.Telegram.Models;

namespace DateBot.Application.Parsing.BotNotifications;

/// <summary>Service message of the bot; stored, no reaction.</summary>
public sealed record IgnoredNotification(RawBotMessage Source, string Reason) : BotNotification(Source);
