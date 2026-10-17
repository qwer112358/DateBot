using DateBot.Application.Telegram;

namespace DateBot.Application.Parsing;

/// <summary>"Someone liked you" without the profile: who it is is visible only in the app, so check the app's likes.</summary>
public sealed record LikeHintNotification(RawBotMessage Source) : BotNotification(Source);
