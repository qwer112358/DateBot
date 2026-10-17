using CSharpFunctionalExtensions;
using DateBot.Application.Parsing;
using DateBot.Application.Processing;
using DateBot.Application.Replies;
using DateBot.Application.Telegram;
using DateBot.Domain.Matches;
using DateBot.Infrastructure.MiniApp;
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
        new MiniAppClientOptionsValidator().Validate(Bind<MiniAppClientOptions>(MiniAppClientOptions.Section)).Errors.Should().BeEmpty();
    }

    [Fact]
    public void Shipped_settings_let_you_pick_Telegram_or_the_app_for_every_match()
    {
        var processing = Bind<ProcessingOptions>(ProcessingOptions.Section);

        processing.Mode.Should().Be(ProcessingMode.Confirm);
        processing.Channels.Should().Equal(MessageChannel.Telegram, MessageChannel.App);
        processing.MiniApp.Enabled.Should().BeTrue();
        processing.MiniApp.PollInterval.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Shipped_settings_treat_like_hints_as_hints()
    {
        var rules = ParsingRules.From(Bind<ParsingOptions>(ParsingOptions.Section));
        var hint = new RawBotMessage(
            50, DateTimeOffset.UtcNow, "💌 Тобой интересуются\n\nЗагляни в приложение — вдруг это взаимно.",
            [new BotButton("💛 Открыть знакомства", 0, 0)], Maybe<string>.None);

        BotNotificationParser.Parse(hint, rules).Should().BeOfType<LikeHintNotification>();
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

        errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["ApiId", "ApiHash", "PhoneNumber"]);
    }

    [Fact]
    public void Shipped_settings_parse_the_real_match_card()
    {
        var rules = ParsingRules.From(Bind<ParsingOptions>(ParsingOptions.Section));
        var card = new RawBotMessage(
            40,
            DateTimeOffset.UtcNow,
            "❤️ У вас мэтч с Аня!\n\nВы оба лайкнули друг друга. Самое время написать.\n@anya_sample",
            [new BotButton("💛 Открыть знакомства", 0, 0)],
            Maybe.From("photo:222"));

        var mutual = BotNotificationParser.Parse(card, rules).Should().BeOfType<MutualNotification>().Subject;

        mutual.Profile.Name.Should().Be("Аня");
        ProfileMapper.PersonKey(mutual.Profile).Should().Be("id:anya_sample");
        Bind<TelegramOptions>(TelegramOptions.Section).TashdateBotUsername.Should().Be("Znakomstva_Tashkentt_bot");
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
