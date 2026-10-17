using DateBot.Application.Telegram;

namespace DateBot.Application.Parsing;

/// <summary>"Mutual like": scenario B.</summary>
public sealed record MutualNotification(RawBotMessage Source, ParsedProfile Profile) : BotNotification(Source);
