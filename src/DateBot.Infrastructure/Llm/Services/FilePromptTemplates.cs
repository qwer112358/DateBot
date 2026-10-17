using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Models;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Infrastructure.Platform.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Llm.Services;

/// <summary>
/// Reads <see cref="ReplyOptions.PromptsFile"/> on every call, so a saved edit applies to the next message; where the
/// file is looked up is <see cref="ContentFiles"/>' call.
/// </summary>
public sealed class FilePromptTemplates(IOptions<ReplyOptions> options, IHostEnvironment environment) : IPromptTemplates
{
    public string FilePath => ContentFiles.Resolve(environment, options.Value.PromptsFile);

    public async Task<Result<PromptTemplates, Error>> LoadAsync(CancellationToken ct)
    {
        var path = FilePath;
        try
        {
            return PromptTemplateParser.Parse(await File.ReadAllTextAsync(path, ct));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error.External("prompts.unreadable", $"Не читается файл промптов {path}: {ex.Message}");
        }
    }
}
