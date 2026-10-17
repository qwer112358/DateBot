using DateBot.Application.Replies.Enums;
using DateBot.Application.Replies.Options;
using DateBot.Infrastructure.Llm.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Tests.Llm;

public sealed class FileLlmSettingsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datebot-llm-").FullName;

    [Fact]
    public async Task Shipped_file_is_read_from_the_project_folder_and_is_valid()
    {
        var settings = Settings(WorkerFolder());

        var loaded = await settings.LoadAsync(default);

        settings.FilePath.Should().Be(Path.Combine(WorkerFolder(), "llm.json"));
        loaded.Value.Model.Should().NotBeEmpty();
        loaded.Value.Templates.Keys.Should().BeEquivalentTo("Russian", "Uzbek", "English");
    }

    [Fact]
    public async Task Saved_edit_applies_on_the_next_call()
    {
        var file = await CopyShippedAsync();
        var settings = Settings(_folder);
        await settings.LoadAsync(default);

        await File.WriteAllTextAsync(file, (await File.ReadAllTextAsync(file))
            .Replace("\"Effort\": \"Low\"", "\"Effort\": \"High\"", StringComparison.Ordinal)
            .Replace("\"MaxPhotos\": 10", "\"MaxPhotos\": 3", StringComparison.Ordinal));

        var loaded = (await settings.LoadAsync(default)).Value;
        loaded.Effort.Should().Be(ModelEffort.High);
        loaded.MaxPhotos.Should().Be(3);
    }

    [Theory]
    [InlineData("\"MaxPhotos\": 10", "\"MaxPhotos\": 42")]
    [InlineData("\"Effort\": \"Low\"", "\"Effort\": \"Turbo\"")]
    [InlineData("\"MaxPhotos\": 10,", "\"MaxPhotos\": 10,,,")]
    public async Task Broken_edit_keeps_the_last_good_settings(string from, string to)
    {
        var file = await CopyShippedAsync();
        var settings = Settings(_folder);
        var good = (await settings.LoadAsync(default)).Value;

        await File.WriteAllTextAsync(file, (await File.ReadAllTextAsync(file)).Replace(from, to, StringComparison.Ordinal));

        (await settings.LoadAsync(default)).Value.Should().Be(good);
    }

    [Fact]
    public async Task File_broken_from_the_start_is_an_error_naming_the_problem()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "llm.json"), """{ "MaxPhotos": 42 }""");

        var loaded = await Settings(_folder).LoadAsync(default);

        loaded.Error.Message.Should().Contain("MaxPhotos");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private async Task<string> CopyShippedAsync()
    {
        var file = Path.Combine(_folder, "llm.json");
        await File.WriteAllTextAsync(file, await File.ReadAllTextAsync(Path.Combine(WorkerFolder(), "llm.json")));
        return file;
    }

    private static FileLlmSettings Settings(string contentRoot) =>
        new(Options.Create(new ReplyOptions()), new Environment { ContentRootPath = contentRoot }, NullLogger<FileLlmSettings>.Instance);

    private static string WorkerFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DateBot.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found"), "src", "DateBot.Worker");
    }

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "DateBot";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
