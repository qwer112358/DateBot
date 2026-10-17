using CSharpFunctionalExtensions;
using DateBot.Application.Parsing;
using DateBot.Domain.Matches;
using static DateBot.Application.Tests.Samples;

namespace DateBot.Application.Tests;

public sealed class ParserTests
{
    private static readonly ParsingRules Rules = TestOptions.Rules();

    [Fact]
    public void Like_notification_is_parsed_with_profile_and_photo()
    {
        var notification = BotNotificationParser.Parse(Like(10), Rules);

        var like = notification.Should().BeOfType<LikeNotification>().Subject;
        like.Profile.Name.Should().Be("Анна");
        like.Profile.Age.Should().Be(Maybe.From(24));
        like.Profile.City.Should().Be(Maybe.From("Ташкент"));
        like.Profile.Bio.GetValueOrThrow().Should().Be("Люблю горы, кофе и долгие прогулки");
        like.Profile.PhotoId.Should().Be(Maybe.From("photo:111"));
        like.Profile.Username.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Mutual_notification_wins_over_like_markers_and_has_username()
    {
        var notification = BotNotificationParser.Parse(Mutual(11), Rules);

        var mutual = notification.Should().BeOfType<MutualNotification>().Subject;
        mutual.Profile.Name.Should().Be("Анна");
        mutual.Profile.Username.Should().Be(Maybe.From("anna_tash"));
    }

    [Fact]
    public void Profile_without_city_and_bio_is_parsed()
    {
        var like = (LikeNotification)BotNotificationParser.Parse(Like(12, "Дилноза, 22"), Rules);

        like.Profile.Name.Should().Be("Дилноза");
        like.Profile.Age.Should().Be(Maybe.From(22));
        like.Profile.City.HasNoValue.Should().BeTrue();
        like.Profile.Bio.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Uzbek_profile_with_interests_line()
    {
        var text = "Sizni kimdir yoqtirdi!\n\nMadina, 23, Samarqand – Kitob o'qishni va sayohatni yaxshi ko'raman\nQiziqishlar: kitoblar, sayohat, musiqa";

        var like = (LikeNotification)BotNotificationParser.Parse(Text(13, text), Rules);

        like.Profile.Name.Should().Be("Madina");
        like.Profile.City.Should().Be(Maybe.From("Samarqand"));
        like.Profile.Interests.Should().Equal("kitoblar", "sayohat", "musiqa");
        like.Profile.Bio.GetValueOrThrow().Should().Be("Kitob o'qishni va sayohatni yaxshi ko'raman");
    }

    [Fact]
    public void Message_without_markers_goes_to_parse_failed()
    {
        BotNotificationParser.Parse(Text(14, "Добро пожаловать в Tashdate!"), Rules)
            .Should().BeOfType<UnknownNotification>();
    }

    [Fact]
    public void Message_without_markers_can_be_ignored_by_policy()
    {
        var rules = Rules with { UnknownMessages = UnknownMessagePolicy.Ignore };

        BotNotificationParser.Parse(Text(15, "Добро пожаловать в Tashdate!"), rules)
            .Should().BeOfType<IgnoredNotification>();
    }

    [Fact]
    public void Marker_without_profile_goes_to_parse_failed()
    {
        BotNotificationParser.Parse(Text(16, "Ты понравился 3 людям, показать их?"), Rules)
            .Should().BeOfType<UnknownNotification>()
            .Which.Reason.Should().Contain("анкет");
    }

    [Fact]
    public void Service_message_is_ignored()
    {
        BotNotificationParser.Parse(Text(17, "Лайк отправлен, ждём ответа ✨"), Rules)
            .Should().BeOfType<IgnoredNotification>();
    }

    [Fact]
    public void Same_person_in_like_and_mutual_gets_the_same_id()
    {
        var like = (LikeNotification)BotNotificationParser.Parse(Like(20), Rules);
        var mutual = (MutualNotification)BotNotificationParser.Parse(Mutual(21), Rules);

        ProfileMapper.PersonKey(like.Profile).Should().Be(ProfileMapper.PersonKey(mutual.Profile));
        ProfileMapper.PersonKey(like.Profile).Should().StartWith("fp:");
    }

    [Fact]
    public void Different_people_get_different_ids()
    {
        var anna = (LikeNotification)BotNotificationParser.Parse(Like(20), Rules);
        var other = (LikeNotification)BotNotificationParser.Parse(Like(22, "Анна, 25, Ташкент – Люблю горы"), Rules);

        ProfileMapper.PersonKey(anna.Profile).Should().NotBe(ProfileMapper.PersonKey(other.Profile));
    }

    [Fact]
    public void Explicit_person_id_from_rules_is_used()
    {
        var rules = ParsingRules.From(new ParsingOptions
        {
            LikeMarkers = ["понравил"],
            MutualMarkers = ["взаимн"],
            ProfilePattern = TestOptions.Parsing().ProfilePattern,
            PersonIdPattern = @"ID:\s*(?<id>\d+)",
        });
        var like = (LikeNotification)BotNotificationParser.Parse(Text(23, $"Ты понравился!\n{AnnaProfile}\nID: 98765"), rules);

        ProfileMapper.PersonKey(like.Profile).Should().Be("id:98765");
    }

    [Fact]
    public void Minor_profile_is_rejected_by_the_domain()
    {
        var like = (LikeNotification)BotNotificationParser.Parse(Like(24, "Лена, 17, Ташкент – школа"), Rules);

        var mapped = ProfileMapper.ToDomain(like.Profile, "raw");

        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Details.Should().Contain(d => d.PropertyName == nameof(Age));
    }

    [Fact]
    public void Mapping_collects_all_violations()
    {
        var parsed = new ParsedProfile(
            new string('x', 80), Maybe.From(150), Maybe<string>.None, Maybe<string>.None, [], Maybe<string>.None,
            Maybe.From("bad name"), Maybe<string>.None);

        var mapped = ProfileMapper.ToDomain(parsed, "raw");

        mapped.Error.Details.Select(d => d.PropertyName).Should().Contain([nameof(ProfileName), nameof(Age), nameof(ContactUsername)]);
    }
}
