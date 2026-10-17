using DateBot.Application.Replies.Models;
using DateBot.Application.Replies.Services;

namespace DateBot.Application.Tests.Support;

/// <summary>The prompts file the service ships with, so every test also proves it parses.</summary>
internal static class ShippedPrompts
{
    public static string Text { get; } = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "DateBot.Worker", "prompts.md"));

    public static PromptTemplates Templates { get; } = PromptTemplateParser.Parse(Text).Value;

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DateBot.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
