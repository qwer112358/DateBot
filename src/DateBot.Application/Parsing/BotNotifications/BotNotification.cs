using DateBot.Application.Telegram.Models;

namespace DateBot.Application.Parsing.BotNotifications;

public abstract record BotNotification(RawBotMessage Source);
