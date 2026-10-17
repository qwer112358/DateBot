using CSharpFunctionalExtensions;
using DateBot.Application.Replies.Models;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Ports.Services;

/// <summary>LLM that writes the first message.</summary>
public interface IReplyGenerator
{
    Task<Result<string, Error>> GenerateAsync(ReplyPrompt prompt, Maybe<ProfilePhoto> photo, CancellationToken ct);
}
