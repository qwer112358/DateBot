using DateBot.Application.Telegram;

namespace DateBot.Application.Parsing;

/// <summary>"Someone liked you": scenario A.</summary>
public sealed record LikeNotification(RawBotMessage Source, ParsedProfile Profile) : BotNotification(Source);
