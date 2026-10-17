using CSharpFunctionalExtensions;
using DateBot.Application.Replies;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;

namespace DateBot.Application.Ports;

/// <summary>LLM that writes the first message.</summary>
public interface IReplyGenerator
{
    Task<Result<string, Error>> GenerateAsync(ReplyPrompt prompt, Maybe<ProfilePhoto> photo, CancellationToken ct);
}
