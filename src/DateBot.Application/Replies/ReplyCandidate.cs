namespace DateBot.Application.Replies;

public sealed record ReplyCandidate(string Text, ReplyLanguage ExpectedLanguage, int MinLength, int MaxLength, int MaxEmoji);
