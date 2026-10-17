namespace DateBot.Infrastructure.MiniApp.Enums;

/// <summary>How a failed request is judged: a repeated read or like is harmless, a repeated chat message is not.</summary>
internal enum AppRequestKind
{
    Read,
    Like,
    ChatMessage,
}
