using DateBot.Application.Parsing;
using DateBot.Application.Processing;
using DateBot.Application.Replies;
using Microsoft.Extensions.Configuration;

namespace DateBot.Infrastructure.Tests;

/// <summary>The shipped appsettings.json binds and passes validation once the secrets are filled in.</summary>
public sealed class ConfigurationTests
{
    private static readonly IConfiguration Shipped = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(FindRepositoryRoot(), "src", "DateBot.Worker", "appsettings.json"))
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telegram:ApiId"] = "12345",
            ["Telegram:ApiHash"] = "0123456789abcdef0123456789abcdef",
            ["Telegram:PhoneNumber"] = "+998901234567",
            ["Telegram:TashdateBotUsername"] = "tashdate_bot",
            ["Replies:Owner:Name"] = "Рустам",
        })
        .Build();

    [Fact]
    public void Shipped_settings_are_valid_with_secrets()
    {
        new ProcessingOptionsValidator().Validate(Bind<ProcessingOptions>(ProcessingOptions.Section)).Errors.Should().BeEmpty();
        new ParsingOptionsValidator().Validate(Bind<ParsingOptions>(ParsingOptions.Section)).Errors.Should().BeEmpty();
        new ReplyOptionsValidator().Validate(Bind<ReplyOptions>(ReplyOptions.Section)).Errors.Should().BeEmpty();
        new TelegramOptionsValidator().Validate(Bind<TelegramOptions>(TelegramOptions.Section)).Errors.Should().BeEmpty();
        new NotifierOptionsValidator().Validate(Bind<NotifierOptions>(NotifierOptions.Section)).Errors.Should().BeEmpty();
        new AnthropicOptionsValidator().Validate(Bind<AnthropicOptions>(AnthropicOptions.Section)).Errors.Should().BeEmpty();
        new StorageOptionsValidator().Validate(Bind<StorageOptions>(StorageOptions.Section)).Errors.Should().BeEmpty();
    }

    [Fact]
    public void Shipped_settings_bind_lists_and_times()
    {
        var processing = Bind<ProcessingOptions>(ProcessingOptions.Section);

        processing.LikeBackButtons.Should().Contain("❤");
        processing.MaxPause.Should().Be(TimeSpan.FromSeconds(30));
        Bind<ReplyOptions>(ReplyOptions.Section).Templates.Keys.Should().BeEquivalentTo(["Russian", "Uzbek", "English"]);
    }

    [Fact]
    public void Missing_secrets_are_reported()
    {
        var empty = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(FindRepositoryRoot(), "src", "DateBot.Worker", "appsettings.json"))
            .Build();

        var errors = new TelegramOptionsValidator().Validate(empty.GetSection(TelegramOptions.Section).Get<TelegramOptions>()!).Errors;

        errors.Select(e => e.PropertyName).Should().Contain(["ApiId", "ApiHash", "PhoneNumber", "TashdateBotUsername"]);
    }

    [Fact]
    public void Inverted_pauses_are_rejected() =>
        new ProcessingOptionsValidator()
            .Validate(new ProcessingOptions { LikeBackButtons = ["❤"], MinPause = TimeSpan.FromSeconds(30), MaxPause = TimeSpan.FromSeconds(5) })
            .Errors.Should().Contain(e => e.PropertyName == "MaxPause");

    private static T Bind<T>(string section) => Shipped.GetSection(section).Get<T>()!;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DateBot.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
