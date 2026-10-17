using DateBot.Application.Telegram;

namespace DateBot.Application.Parsing;

public abstract record BotNotification(RawBotMessage Source);
