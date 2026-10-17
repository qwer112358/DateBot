namespace DateBot.Application.Telegram.Models;

/// <summary>Downloaded profile photo.</summary>
public sealed record ProfilePhoto(ReadOnlyMemory<byte> Content, string MediaType);
