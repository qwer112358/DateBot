using DateBot.Application.Replies.Enums;

namespace DateBot.Application.Replies.Models;

public sealed record ReplyCandidate(string Text, ReplyLanguage ExpectedLanguage, int MinLength, int MaxLength, int MaxEmoji);
