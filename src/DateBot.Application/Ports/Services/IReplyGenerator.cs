using CSharpFunctionalExtensions;
using DateBot.Application.Replies.Models;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Ports.Services;

/// <summary>LLM that writes the first message.</summary>
public interface IReplyGenerator
{
    /// <param name="photos">The profile photos, main first; empty when the model gets none.</param>
    /// <param name="settings">Which model, how much it thinks, the answer limit.</param>
    Task<Result<string, Error>> GenerateAsync(ReplyPrompt prompt, IReadOnlyList<ProfilePhoto> photos, LlmSettings settings, CancellationToken ct);
}
