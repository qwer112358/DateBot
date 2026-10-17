using CSharpFunctionalExtensions;
using DateBot.Application.Replies.Models;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Ports.Services;

/// <summary>The generation settings you edit; read before every message, so an edit applies without a restart.</summary>
public interface ILlmSettings
{
    Task<Result<LlmSettings, Error>> LoadAsync(CancellationToken ct);
}
