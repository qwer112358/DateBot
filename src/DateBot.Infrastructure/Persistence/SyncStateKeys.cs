namespace DateBot.Infrastructure.Persistence;

public static class SyncStateKeys
{
    public const string LastBotMessageId = "last_bot_message_id";
    public const string LastProcessedAt = "last_processed_at";
    public const string Paused = "paused";
    public const string OwnerUserId = "owner_user_id";
    public const string OwnerChatId = "owner_chat_id";
}
