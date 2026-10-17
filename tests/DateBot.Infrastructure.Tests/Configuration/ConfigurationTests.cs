using CSharpFunctionalExtensions;
using DateBot.Application.Parsing.BotNotifications;
using DateBot.Application.Parsing.Models;
using DateBot.Application.Parsing.Options;
using DateBot.Application.Parsing.Services;
using DateBot.Application.Parsing.Validators;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Processing.Options;
using DateBot.Application.Processing.Validators;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Validators;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Matches.Enums;
using DateBot.Infrastructure.Llm.Options;
using DateBot.Infrastructure.Llm.Validators;
using DateBot.Infrastructure.MiniApp.Options;
using DateBot.Infrastructure.MiniApp.Validators;
using DateBot.Infrastructure.Notifier.Options;
using DateBot.Infrastructure.Notifier.Validators;
using DateBot.Infrastructure.Platform.Options;
using DateBot.Infrastructure.Platform.Validators;
using DateBot.Infrastructure.Telegram.Options;
using DateBot.Infrastructure.Telegram.Validators;
using Microsoft.Extensions.Configuration;

namespace DateBot.Infrastructure.Tests.Configuration;

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

    [Theory]
    [InlineData("💌 Тобой интересуются\n\nЗагляни в приложение — вдруг это взаимно.", "💛 Открыть знакомства")]
    [InlineData("🔥 Рослан, тебя лайкнули 2 раза за последние 24 часа\n\nОткрой приложение — посмотри кто и ответь лайком", "❤️ Посмотреть кто лайкнул")]
    public void Shipped_settings_treat_like_hints_as_hints(string text, string button)
    {
        var rules = ParsingRules.From(Bind<ParsingOptions>(ParsingOptions.Section));
        var hint = new RawBotMessage(50, DateTimeOffset.UtcNow, text, [new BotButton(button, 0, 0)], Maybe<string>.None);

        BotNotificationParser.Parse(hint, rules).Should().BeOfType<LikeHintNotification>();
    }

    [Fact]
    public void Shipped_settings_parse_a_match_card_without_username()
    {
        var rules = ParsingRules.From(Bind<ParsingOptions>(ParsingOptions.Section));
        var card = new RawBotMessage(
            41,
            DateTimeOffset.UtcNow,
            "❤️ У вас мэтч с Шахноза!\n\nВы оба лайкнули друг друга. Самое время написать.",
            [new BotButton("💛 Открыть знакомства", 0, 0)],
            Maybe.From("photo:333"));

        var mutual = BotNotificationParser.Parse(card, rules).Should().BeOfType<MutualNotification>().Subject;

        mutual.Profile.Name.Should().Be("Шахноза");
        mutual.Profile.Username.HasNoValue.Should().BeTrue();
        ProfileMapper.PersonKey(mutual.Profile).Should().StartWith("fp:");
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
