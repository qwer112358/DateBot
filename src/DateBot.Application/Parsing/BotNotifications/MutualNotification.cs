using DateBot.Application.Parsing.Models;
using DateBot.Application.Telegram.Models;

namespace DateBot.Application.Parsing.BotNotifications;

/// <summary>"Mutual like": scenario B.</summary>
public sealed record MutualNotification(RawBotMessage Source, ParsedProfile Profile) : BotNotification(Source);
