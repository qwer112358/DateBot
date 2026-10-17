using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.UseCases;
using DateBot.Domain.Matches;
using DateBot.Infrastructure.Notifier;
using DateBot.Infrastructure.Platform;

namespace DateBot.Infrastructure.Tests;

public sealed class NotifierTests
{
    private static readonly MatchId Id = MatchId.Create(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")).Value;

    [Fact]
    public void Draft_has_send_and_skip_buttons_and_the_photo()
    {
        var formatted = NotificationFormatter.Format(new DraftReadyNotification(
            Id, MatchKind.LikedMe, Profile("<b>Анна</b>"), ReplyText.Create("Привет! Как дела?").Value));

        formatted.Buttons.Select(b => b.Data).Should().Equal(
            "send:0f8fad5bd9cb469fa16570867728950e", "skip:0f8fad5bd9cb469fa16570867728950e");
        formatted.Photo.Should().Be(Maybe.From(PhotoFileId.Create("photo:1").Value));
        formatted.Html.Should().Contain("&lt;b&gt;Анна&lt;/b&gt;", "profile text is escaped");
    }

    [Fact]
    public void Callback_data_round_trips_to_owner_requests()
    {
        var formatted = NotificationFormatter.Format(new DraftReadyNotification(
            Id, MatchKind.Mutual, Profile("Анна"), ReplyText.Create("Привет! Как дела?").Value));

        OwnerBotPoller.ParseCallback(7, 42, formatted.Buttons[0].Data).GetValueOrThrow()
            .Should().Be(new DraftDecisionRequest(7, 42, Id.Value, Approve: true));
        OwnerBotPoller.ParseCallback(7, 42, formatted.Buttons[1].Data).GetValueOrThrow()
            .Should().Be(new DraftDecisionRequest(7, 42, Id.Value, Approve: false));
        OwnerBotPoller.ParseCallback(7, 42, "forget:" + Id.Value.ToString("N")).GetValueOrThrow()
            .Should().Be(new ForgetRequest(7, 42, Id.Value));
        OwnerBotPoller.ParseCallback(7, 42, "garbage").HasNoValue.Should().BeTrue();
    }

    [Theory]
    [InlineData("/start", typeof(StartRequest))]
    [InlineData("/stop", typeof(StopRequest))]
    [InlineData("/status@my_notifier_bot", typeof(StatusRequest))]
    [InlineData("привет", typeof(UnknownRequest))]
    public void Commands_are_parsed(string text, Type expected) =>
        OwnerBotPoller.ParseCommand(7, 42, text).Should().BeOfType(expected);

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0 }, "image/png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg")]
    public void Photo_media_type_is_detected(byte[] bytes, string expected) =>
        FilePhotoStore.DetectMediaType(bytes).Should().Be(expected);

    private static Profile Profile(string name) =>
        Domain.Matches.Profile.Create(new ProfileInput(
            ProfileName.Create(name).Value,
            Maybe.From(Age.Create(24).Value),
            Maybe<City>.None,
            Maybe.From(Bio.Create("Люблю горы").Value),
            Interests.Empty,
            Maybe.From(PhotoFileId.Create("photo:1").Value),
            Maybe<ContactUsername>.None,
            name)).Value;
}
