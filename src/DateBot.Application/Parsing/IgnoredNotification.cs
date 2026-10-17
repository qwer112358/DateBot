using DateBot.Application.Telegram;

namespace DateBot.Application.Parsing;

/// <summary>Service message of the bot; stored, no reaction.</summary>
public sealed record IgnoredNotification(RawBotMessage Source, string Reason) : BotNotification(Source);
