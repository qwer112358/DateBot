using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.Parsing.BotNotifications;
using DateBot.Application.Parsing.Models;
using DateBot.Application.Parsing.Services;
using DateBot.Application.Telegram.Models;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases.Enums;
using DateBot.Domain.Inbox.Enums;
using static DateBot.Application.Tests.Support.Samples;

namespace DateBot.Application.Tests.Parsing;

/// <summary>The real notification format of @Znakomstva_Tashkentt_bot: a match card with name, @username and photo.</summary>
public sealed class TashdateFormatTests
{
    private static readonly ParsingRules Rules = ParsingRules.From(TestOptions.TashdateParsing());

    [Fact]
    public void Match_card_is_a_mutual_notification_with_name_username_and_photo()
    {
        var mutual = BotNotificationParser.Parse(TashdateMatch(40), Rules).Should().BeOfType<MutualNotification>().Subject;

        mutual.Profile.Name.Should().Be("Аня");
        mutual.Profile.Username.Should().Be(Maybe.From("anya_sample"));
        mutual.Profile.PhotoId.Should().Be(Maybe.From("photo:222"));
        mutual.Profile.Age.HasNoValue.Should().BeTrue();
        ProfileMapper.PersonKey(mutual.Profile).Should().Be("id:anya_sample");
    }

    [Theory]
    [InlineData("💌 Тобой интересуются\n\nЗагляни в приложение — вдруг это взаимно.")]
    [InlineData("🔥 Рустам, тебя лайкнули 2 раза за последние 24 часа\n\nОткрой приложение — посмотри кто и ответь лайком")]
    public void Like_hints_without_a_profile_are_hints_not_parse_failures(string text)
    {
        var message = Text(50, text) with { Buttons = [new BotButton("💛 Открыть знакомства", 0, 0)] };

        BotNotificationParser.Parse(message, Rules).Should().BeOfType<LikeHintNotification>();
    }

    [Fact]
    public async Task Like_hint_is_stored_as_ignored_and_counted_so_the_app_is_checked()
    {
        await using var host = new TestHost(parsing: TestOptions.TashdateParsing());
        host.Client.History.Add(Text(50, "💌 Тобой интересуются\n\nЗагляни в приложение — вдруг это взаимно."));

        var report = await host.CatchUpAsync();

        report.Value.LikeHints.Should().Be(1);
        host.Store.Inbox[50].Status.Should().Be(InboxStatus.Ignored);
        host.Notifier.Sent.OfType<ParseFailedNotification>().Should().BeEmpty();
    }

    [Fact]
    public void Names_with_spaces_and_emoji_are_kept()
    {
        var mutual = (MutualNotification)BotNotificationParser.Parse(TashdateMatch(41, "Мария Анна 🌸"), Rules);

        mutual.Profile.Name.Should().Be("Мария Анна 🌸");
    }

    [Fact]
    public void Two_people_with_the_same_name_are_different_people()
    {
        var first = (MutualNotification)BotNotificationParser.Parse(TashdateMatch(42, "Аня", "anya_sample"), Rules);
        var second = (MutualNotification)BotNotificationParser.Parse(TashdateMatch(43, "Аня", "other_anya"), Rules);

        ProfileMapper.PersonKey(first.Profile).Should().NotBe(ProfileMapper.PersonKey(second.Profile));
    }

    [Fact]
    public async Task Match_card_gets_a_direct_message_written_with_the_photo()
    {
        await using var host = new TestHost(parsing: TestOptions.TashdateParsing());
        host.Client.Photo = Maybe.From(new ProfilePhoto(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg"));

        var outcome = await host.ProcessAsync(TashdateMatch(40));

        outcome.Value.Should().Be(ProcessOutcome.Done);
        host.Client.Presses.Should().BeEmpty("a match needs no like back");
        host.Client.SentToUsers.Should().ContainSingle().Which.Username.Should().Be("anya_sample");
        host.Generator.Calls.Single().Photo.HasValue.Should().BeTrue();
        host.Generator.Calls.Single().Prompt.User.Should().Contain("Имя: Аня");
        host.Notifier.Sent.OfType<MutualHandledNotification>().Should().ContainSingle();
    }

    [Fact]
    public async Task Second_match_card_for_the_same_username_is_ignored()
    {
        await using var host = new TestHost(parsing: TestOptions.TashdateParsing());

        await host.ProcessAsync(TashdateMatch(40));
        var again = await host.ProcessAsync(TashdateMatch(44));

        again.Value.Should().Be(ProcessOutcome.Ignored);
        host.Client.Sends.Should().Be(1);
    }
}
