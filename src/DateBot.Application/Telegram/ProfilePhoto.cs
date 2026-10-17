namespace DateBot.Application.Telegram;

/// <summary>Downloaded profile photo.</summary>
public sealed record ProfilePhoto(ReadOnlyMemory<byte> Content, string MediaType);
