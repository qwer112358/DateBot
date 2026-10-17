using DateBot.Application.Parsing.Models;
using DateBot.Application.Telegram.Models;

namespace DateBot.Application.Parsing.BotNotifications;

/// <summary>"Someone liked you": scenario A.</summary>
public sealed record LikeNotification(RawBotMessage Source, ParsedProfile Profile) : BotNotification(Source);
