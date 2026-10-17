using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.Parsing;
using DateBot.Application.Telegram;
using DateBot.Application.UseCases;
using static DateBot.Application.Tests.Samples;

namespace DateBot.Application.Tests;

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
