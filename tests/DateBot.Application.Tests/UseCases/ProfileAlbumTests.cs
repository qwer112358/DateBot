using DateBot.Application.MiniApp.Enums;
using DateBot.Application.Notifications;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.OwnerRequests;

namespace DateBot.Application.Tests.UseCases;

/// <summary>
/// All the app profile's photos reach you: stored with the person and shown as an album
/// with the first card about them.
/// </summary>
public sealed class ProfileAlbumTests
{
    [Fact]
    public async Task Every_app_photo_is_stored_and_the_like_card_carries_them()
    {
        await using var host = Host(LikeBackMode.Confirm);
        host.App.AddLike(501, "Лола", extraPhotos: 3);

        await host.SyncAppAsync();

        var card = host.Notifier.Sent.OfType<LikeBackProposedNotification>().Single();
        card.Profile.ExtraPhotos.Items.Select(p => p.Value).Should().Equal("app:p501-2.jpg", "app:p501-3.jpg", "app:p501-4.jpg");
        host.Photos.Photos.Keys.Should().BeEquivalentTo("app:p501.jpg", "app:p501-2.jpg", "app:p501-3.jpg", "app:p501-4.jpg");
    }

    [Fact]
    public async Task Draft_after_a_like_card_does_not_repeat_the_album()
    {
        await using var host = Host(LikeBackMode.Confirm);
        host.App.AddLike(501, "Лола", extraPhotos: 2);
        await host.SyncAppAsync();
        await host.OwnerAsync(new LikeBackDecisionRequest(7, 42, host.Store.SingleMatch.Id.Value, Approve: true));

        await host.SyncAppAsync();

        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().WithAlbum.Should().BeFalse("the like card just above showed it");
    }

    [Fact]
    public async Task Without_like_cards_the_first_draft_brings_the_album_and_an_edit_does_not()
    {
        await using var host = Host(LikeBackMode.Auto);
        host.App.AddLike(501, "Лола", extraPhotos: 2);
        await host.SyncAppAsync();

        await host.OwnerAsync(new EditDraftRequest(7, 42, host.Store.SingleMatch.Id.Value, DraftEditKind.OwnText));
        await host.OwnerAsync(new UnknownRequest(7, 42, "Привет, Лола! Тоже люблю горы. Куда ходила последний раз?"));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Select(d => d.WithAlbum).Should().Equal(true, false);
    }

    [Fact]
    public async Task Model_sees_every_photo_main_first()
    {
        await using var host = Host(LikeBackMode.Auto);
        host.App.AddLike(501, "Лола", extraPhotos: 2);

        await host.SyncAppAsync();

        host.Generator.Calls.Single().Photos.Should().HaveCount(3);
    }

    [Fact]
    public async Task Likes_command_sends_waiting_cards_again_with_their_album()
    {
        await using var host = Host(LikeBackMode.Confirm);
        host.App.AddLike(501, "Лола", extraPhotos: 2);
        await host.SyncAppAsync();

        await host.SyncAppAsync();
        var resent = await host.SyncAppAsync(resendWaiting: true);

        resent.Value.Waiting.Should().Be(1);
        host.Notifier.Sent.OfType<LikeBackProposedNotification>().Should().HaveCount(2, "a plain pass does not repeat the card")
            .And.OnlyContain(card => card.Profile.ExtraPhotos.Items.Length == 2);
    }

    [Fact]
    public async Task Card_from_before_albums_gets_its_photos_when_sent_again()
    {
        await using var host = Host(LikeBackMode.Confirm);
        host.App.AddLike(501, "Лола");
        await host.SyncAppAsync();
        host.App.Profiles[501] = host.App.Profiles[501] with { PhotoFiles = ["p501.jpg", "p501-2.jpg", "p501-3.jpg"] };

        await host.SyncAppAsync(resendWaiting: true);

        host.Notifier.Sent.OfType<LikeBackProposedNotification>().Last().Profile.ExtraPhotos.Items
            .Select(p => p.Value).Should().Equal("app:p501-2.jpg", "app:p501-3.jpg");
        host.Store.SingleMatch.Profile.ExtraPhotos.Items.Should().HaveCount(2, "kept for later cards");
        host.Photos.Photos.Should().ContainKey("app:p501-3.jpg");
    }

    [Fact]
    public async Task Forget_deletes_every_photo()
    {
        await using var host = Host(LikeBackMode.Confirm);
        host.App.AddLike(501, "Лола", extraPhotos: 2);
        await host.SyncAppAsync();

        await host.OwnerAsync(new ForgetRequest(7, 42, host.Store.SingleMatch.Id.Value));

        host.Photos.Photos.Should().BeEmpty();
    }

    private static TestHost Host(LikeBackMode likeBack) =>
        new(processing: TestOptions.TashdateProcessing(likeBack: likeBack), parsing: TestOptions.TashdateParsing());
}
