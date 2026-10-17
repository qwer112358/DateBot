using DateBot.Application.Telegram;

namespace DateBot.Application.Parsing;

/// <summary>Not recognised; goes to ParseFailed for manual review.</summary>
public sealed record UnknownNotification(RawBotMessage Source, string Reason) : BotNotification(Source);
