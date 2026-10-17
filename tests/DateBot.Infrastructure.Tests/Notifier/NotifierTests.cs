using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Infrastructure.Notifier.Services;
using DateBot.Infrastructure.Platform.Services;

namespace DateBot.Infrastructure.Tests.Notifier;

public sealed class NotifierTests
{
    private const string IdN = "0f8fad5bd9cb469fa16570867728950e";
    private static readonly MatchId Id = MatchId.Create(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")).Value;

    [Fact]
    public void Draft_has_a_button_per_channel_skip_and_the_photo()
    {
        var formatted = NotificationFormatter.Format(Draft(Profile("<b>Анна</b>"), MessageChannel.Telegram, MessageChannel.App));

        formatted.Buttons.Select(b => (b.Text, b.Data)).Should().Equal(
            ("✈️ В Telegram", "send:tg:" + IdN),
            ("💬 В приложении", "send:app:" + IdN),
            ("✏️ Изменить", "edit:" + IdN),
            ("🤖 Переписать", "rewrite:" + IdN),
            ("⏭ Пропустить", "skip:" + IdN));
        formatted.Photo.Should().Be(Maybe.From(PhotoFileId.Create("photo:1").Value));
        formatted.Html.Should().Contain("&lt;b&gt;Анна&lt;/b&gt;", "profile text is escaped");
        formatted.Html.Should().Contain("мэтч");
    }

    [Fact]
    public void Draft_shows_the_username_and_the_app_facts()
    {
        var profile = Profile("Лола")
            .WithContact(Maybe.From(ContactUsername.Create("lola_tash").Value), Maybe<AppProfileId>.None)
            .Enrich(Domain.Matches.ValueObjects.Profile.Create(new ProfileInput(
                ProfileName.Create("Лола").Value, Maybe<Age>.None, Maybe<City>.None, Maybe<Bio>.None, Interests.Empty,
                Maybe<PhotoFileId>.None, Maybe<ContactUsername>.None, "Лола", ProfileFacts.Create(["Питомцы: кошка"]).Value)).Value);

        var html = NotificationFormatter.Format(Draft(profile, MessageChannel.Telegram)).Html;

        html.Should().Contain("Telegram: @lola_tash").And.Contain("Питомцы: кошка");
    }

    [Fact]
    public void Every_notification_about_a_person_carries_the_photo()
    {
        var profile = Profile("Анна");
        var text = ReplyText.Create("Привет! Как дела?").Value;
        OwnerNotification[] aboutPerson =
        [
            new LikeHandledNotification(Id, profile, true, text, MessageChannel.Telegram),
            new LikedBackNotification(Id, profile),
            new MutualHandledNotification(Id, profile, text, MessageChannel.App),
            new ActionFailedNotification(Id, profile, Error.External("telegram.rpc", "boom")),
            Draft(profile, MessageChannel.Telegram),
            Draft(profile),
            new LikeBackProposedNotification(Id, profile),
        ];

        aboutPerson.Select(n => NotificationFormatter.Format(n).Photo)
            .Should().AllBeEquivalentTo(Maybe.From(PhotoFileId.Create("photo:1").Value));
        NotificationFormatter.Format(new TextNotification("Отправлено.")).Photo.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Sent_notification_says_where_the_message_went()
    {
        var formatted = NotificationFormatter.Format(new MutualHandledNotification(
            Id, Profile("Анна"), ReplyText.Create("Привет! Как дела?").Value, MessageChannel.App));

        formatted.Html.Should().Contain("в чат приложения");
    }

    [Fact]
    public void Callback_data_round_trips_to_owner_requests()
    {
        var formatted = NotificationFormatter.Format(Draft(Profile("Анна"), MessageChannel.Telegram, MessageChannel.App, MessageChannel.BotChat));

        // A send button only asks; its confirmation is what parses into the send.
        formatted.Buttons.Select(b => OwnerBotPoller.ParseCallback(7, 42, NotificationFormatter.ConfirmSend(b.Data).Map(c => c.Data).GetValueOrDefault(b.Data)).GetValueOrThrow()).Should().Equal(
            new DraftDecisionRequest(7, 42, Id.Value, Approve: true, MessageChannel.Telegram),
            new DraftDecisionRequest(7, 42, Id.Value, Approve: true, MessageChannel.App),
            new DraftDecisionRequest(7, 42, Id.Value, Approve: true, MessageChannel.BotChat),
            new EditDraftRequest(7, 42, Id.Value, DraftEditKind.OwnText),
            new EditDraftRequest(7, 42, Id.Value, DraftEditKind.Hint),
            new DraftDecisionRequest(7, 42, Id.Value, Approve: false));
        formatted.Buttons.Should().OnlyContain(b => b.Data.Length <= 64, "Telegram limits callback data to 64 bytes");
        OwnerBotPoller.ParseCallback(7, 42, "forget:" + IdN).GetValueOrThrow().Should().Be(new ForgetRequest(7, 42, Id.Value));
        OwnerBotPoller.ParseCallback(7, 42, "garbage").HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Draft_no_channel_reaches_asks_to_write_by_hand_and_offers_a_new_search()
    {
        var formatted = NotificationFormatter.Format(Draft(Profile("Шахноза")));

        formatted.Html.Should().Contain("Отправить некуда").And.Contain("«Привет! Как дела?»");
        formatted.Buttons.Select(b => b.Data).Should().Equal("find:" + IdN, "edit:" + IdN, "rewrite:" + IdN, "skip:" + IdN);
        OwnerBotPoller.ParseCallback(7, 42, formatted.Buttons[0].Data).GetValueOrThrow()
            .Should().Be(new LookUpContactsRequest(7, 42, Id.Value));
    }

    [Fact]
    public void Like_card_shows_the_profile_and_its_buttons_round_trip()
    {
        var formatted = NotificationFormatter.Format(new LikeBackProposedNotification(Id, Profile("Лола")));

        formatted.Html.Should().Contain("Вас лайкнули").And.Contain("Лола, 24").And.Contain("Люблю горы");
        formatted.Photo.Should().Be(Maybe.From(PhotoFileId.Create("photo:1").Value));
        formatted.Buttons.Select(b => b.Text).Should().Equal("❤️ Ответить", "⏭ Пропустить");
        formatted.Buttons.Select(b => OwnerBotPoller.ParseCallback(7, 42, b.Data).GetValueOrThrow()).Should().Equal(
            new LikeBackDecisionRequest(7, 42, Id.Value, Approve: true),
            new LikeBackDecisionRequest(7, 42, Id.Value, Approve: false));
        formatted.Buttons.Should().OnlyContain(b => b.Data.Length <= 64);
    }

    [Fact]
    public void Album_goes_with_the_like_card_and_with_a_first_draft_only()
    {
        var album = PhotoAlbum.Create([PhotoFileId.Create("app:2.jpg").Value, PhotoFileId.Create("app:3.jpg").Value]).Value;
        var profile = Profile("Лола").WithExtraPhotos(album);

        NotificationFormatter.Format(new LikeBackProposedNotification(Id, profile)).Album.Should().Equal(album.Items);
        NotificationFormatter.Format(Draft(profile, MessageChannel.App) with { WithAlbum = true }).Album.Should().Equal(album.Items);
        NotificationFormatter.Format(Draft(profile, MessageChannel.App)).Album.Should().BeEmpty();
        NotificationFormatter.Format(new LikedBackNotification(Id, profile)).Album.IsDefaultOrEmpty.Should().BeTrue();
    }

    [Fact]
    public void Likes_summary_lists_only_what_happened()
    {
        var html = NotificationFormatter.Format(new AppLikesCheckedNotification(
            new AppSyncReport(Open: 5, Hidden: 2, Done: 0, Drafted: 1, Ignored: 0, Failed: 0, Proposed: 3, Waiting: 1, Deferred: 0))).Html;

        html.Should().Contain("открытых 5, скрытых до премиума 2")
            .And.Contain("новых карточек: 3")
            .And.Contain("ждут вашего ответа (карточки выше): 1")
            .And.Contain("лайк в ответ поставлен: 1");
        html.Should().NotContain("ошибок").And.NotContain("не дошла очередь");
        NotificationFormatter.Format(new AppLikesCheckedNotification(AppSyncReport.Start(0, 0))).Html
            .Should().Contain("Отвечать пока не на что");
    }

    [Theory]
    [InlineData("edit:", true)]
    [InlineData("rewrite:", true)]
    [InlineData("send:tg:", false)]
    [InlineData("skip:", false)]
    [InlineData("like:", false)]
    public void Only_edit_presses_leave_the_buttons_on_the_card(string prefix, bool keeps) =>
        OwnerBotPoller.KeepsButtons(prefix + IdN).Should().Be(keeps);

    [Fact]
    public void Send_button_of_an_older_card_still_asks_and_then_sends() =>
        OwnerBotPoller.ParseCallback(7, 42, NotificationFormatter.ConfirmSend("send:" + IdN).GetValueOrThrow().Data).GetValueOrThrow()
            .Should().Be(new DraftDecisionRequest(7, 42, Id.Value, Approve: true));

    [Theory]
    [InlineData("send:tg:", "✅ Да, отправить в Telegram", "ok:tg:")]
    [InlineData("send:app:", "✅ Да, отправить в приложение", "ok:app:")]
    public void Send_press_alone_sends_nothing_and_offers_a_confirmation(string prefix, string label, string confirmPrefix)
    {
        OwnerBotPoller.ParseCallback(7, 42, prefix + IdN).HasNoValue.Should().BeTrue("a stray tap must not send");

        var confirm = NotificationFormatter.ConfirmSend(prefix + IdN).GetValueOrThrow();

        confirm.Text.Should().Be(label);
        confirm.Data.Should().Be(confirmPrefix + IdN);
        NotificationFormatter.ConfirmSend("skip:" + IdN).HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Edit_prompt_carries_the_current_text_ready_to_copy() =>
        NotificationFormatter.Format(new EditDraftPromptNotification(ReplyText.Create("Привет <b>Лола</b>!").Value)).Html
            .Should().EndWith("<code>Привет &lt;b&gt;Лола&lt;/b&gt;!</code>");

    [Theory]
    [InlineData("/start", typeof(StartRequest))]
    [InlineData("/stop", typeof(StopRequest))]
    [InlineData("/status@my_notifier_bot", typeof(StatusRequest))]
    [InlineData("/likes", typeof(CheckLikesRequest))]
    [InlineData("/cancel", typeof(CancelRequest))]
    [InlineData("/drafts", typeof(DraftsRequest))]
    [InlineData("/matches", typeof(MatchesRequest))]
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

    private static DraftReadyNotification Draft(Profile profile, params MessageChannel[] channels) =>
        new(Id, MatchKind.Mutual, profile, ReplyText.Create("Привет! Как дела?").Value, Mutual: true, [.. channels]);

    private static Profile Profile(string name) =>
        Domain.Matches.ValueObjects.Profile.Create(new ProfileInput(
            ProfileName.Create(name).Value,
            Maybe.From(Age.Create(24).Value),
            Maybe<City>.None,
            Maybe.From(Bio.Create("Люблю горы").Value),
            Interests.Empty,
            Maybe.From(PhotoFileId.Create("photo:1").Value),
            Maybe<ContactUsername>.None,
            name)).Value;
}
