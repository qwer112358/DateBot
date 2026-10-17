using DateBot.Application.Telegram.Models;

namespace DateBot.Application.Parsing.BotNotifications;

/// <summary>Not recognised; goes to ParseFailed for manual review.</summary>
public sealed record UnknownNotification(RawBotMessage Source, string Reason) : BotNotification(Source);
