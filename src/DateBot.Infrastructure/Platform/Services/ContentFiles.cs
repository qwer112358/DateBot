using Microsoft.Extensions.Hosting;

namespace DateBot.Infrastructure.Platform.Services;

/// <summary>
/// Files you edit while the service runs (prompts.md, llm.json). A relative path is looked up in the content root
/// first: under <c>dotnet run</c> that is the project folder, so the file you edit in the repository is the one read,
/// not its copy in bin. A published app reads the copy next to the exe.
/// </summary>
public static class ContentFiles
{
    public static string Resolve(IHostEnvironment environment, string file)
    {
        if (Path.IsPathRooted(file))
        {
            return file;
        }

        var inProject = Path.Combine(environment.ContentRootPath, file);
        return File.Exists(inProject) ? inProject : Path.Combine(AppContext.BaseDirectory, file);
    }
}
