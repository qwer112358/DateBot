using CSharpFunctionalExtensions;
using DateBot.Application.Replies.Models;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Ports.Services;

/// <summary>The prompts you edit; read before every message, so an edit applies without a restart.</summary>
public interface IPromptTemplates
{
    Task<Result<PromptTemplates, Error>> LoadAsync(CancellationToken ct);
}
