using CSharpFunctionalExtensions;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Models;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Services;
using DateBot.Domain.Common.Errors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Llm.Services;

/// <summary>
/// Reads <see cref="ReplyOptions.PromptsFile"/> on every call, so a saved edit applies to the next message. A relative
/// path is looked up in the content root first: under <c>dotnet run</c> that is the project folder, so the file you edit
/// in the repository is the one read, not its copy in bin.
/// </summary>
public sealed class FilePromptTemplates(IOptions<ReplyOptions> options, IHostEnvironment environment) : IPromptTemplates
{
    public string FilePath
    {
        get
        {
            var file = options.Value.PromptsFile;
            if (Path.IsPathRooted(file))
            {
                return file;
            }

            var inProject = Path.Combine(environment.ContentRootPath, file);
            return File.Exists(inProject) ? inProject : Path.Combine(AppContext.BaseDirectory, file);
        }
    }

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
