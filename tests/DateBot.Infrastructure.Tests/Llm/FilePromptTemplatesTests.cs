using DateBot.Application.Replies.Options;
using DateBot.Infrastructure.Llm.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Tests.Llm;

public sealed class FilePromptTemplatesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datebot-prompts-").FullName;

    [Fact]
    public async Task Shipped_file_is_read_from_the_project_folder()
    {
        var prompts = Prompts(Path.Combine(RepositoryRoot(), "src", "DateBot.Worker"));

        var loaded = await prompts.LoadAsync(default);

        prompts.FilePath.Should().Be(Path.Combine(RepositoryRoot(), "src", "DateBot.Worker", "prompts.md"), "dotnet run edits apply without a rebuild");
        loaded.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Saved_edit_applies_on_the_next_call()
    {
        var shipped = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "src", "DateBot.Worker", "prompts.md"));
        var file = Path.Combine(_folder, "prompts.md");
        await File.WriteAllTextAsync(file, shipped);
        var prompts = Prompts(_folder);
        (await prompts.LoadAsync(default)).Value.FirstMessage.Should().StartWith("Напиши первое сообщение");

        await File.WriteAllTextAsync(file, shipped.Replace("Напиши первое сообщение этому человеку", "Напиши короткое сообщение", StringComparison.Ordinal));

        (await prompts.LoadAsync(default)).Value.FirstMessage.Should().StartWith("Напиши короткое сообщение");
    }

    [Fact]
    public async Task Missing_file_is_an_error_with_its_path()
    {
        var loaded = await Prompts(_folder).LoadAsync(default);

        loaded.Error.Code.Should().Be("prompts.unreadable");
        loaded.Error.Message.Should().Contain("prompts.md");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static FilePromptTemplates Prompts(string contentRoot) =>
        new(Options.Create(new ReplyOptions()), new Environment { ContentRootPath = contentRoot });

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DateBot.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "DateBot";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
