using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Application.Notifications;
using DateBot.Application.Processing.Options;
using DateBot.Application.Telegram.Models;
using DateBot.Application.Tests.Support;
using static DateBot.Application.Tests.Support.Samples;

namespace DateBot.Application.Tests.UseCases;

/// <summary>The owner always sees the person's photo; whether the LLM sees it is a separate setting.</summary>
public sealed class OwnerPhotoTests
{
    private static readonly ProfilePhoto Jpeg = new(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

    [Fact]
    public async Task Photo_is_kept_for_the_owner_even_when_the_LLM_does_not_get_it()
    {
        await using var host = new TestHost(
            processing: TestOptions.TashdateProcessing(), replies: TestOptions.Replies(usePhoto: false), parsing: TestOptions.TashdateParsing());
        host.Client.Photo = Maybe.From(Jpeg);

        await host.ProcessAsync(TashdateMatch(40));

        host.Generator.Calls.Single().Photo.HasNoValue.Should().BeTrue();
        host.Photos.Photos.Should().ContainKey("photo:222");
        host.Notifier.Sent.OfType<DraftReadyNotification>().Single().Profile.Photo.Should().Be(host.Store.SingleMatch.Profile.Photo);
    }

    [Fact]
    public async Task Photo_is_kept_as_soon_as_the_person_appears_so_even_a_failure_shows_it()
    {
        await using var host = new TestHost(processing: TestOptions.TashdateProcessing(), parsing: TestOptions.TashdateParsing());
        host.App.AddLike(501, "Лола");
        host.App.LikeFailures.Enqueue(AppErrors.Refused(400, "already_liked"));

        await host.SyncAppAsync();

        host.Generator.Calls.Should().BeEmpty("nothing was written: the like failed");
        host.Photos.Photos.Should().ContainKey("app:p501.jpg");
        host.Notifier.Sent.OfType<ActionFailedNotification>().Single().Profile.Photo.HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task Declined_person_photo_is_not_downloaded()
    {
        var processing = TestOptions.TashdateProcessing();
        await using var host = new TestHost(
            processing: new ProcessingOptions
            {
                Mode = processing.Mode,
                Channels = processing.Channels,
                MiniApp = processing.MiniApp,
                LikeBackButtons = processing.LikeBackButtons,
                Filters = new FilterOptions { Enabled = true, MinAge = 30, MaxAge = 40 },
            },
            parsing: TestOptions.TashdateParsing());
        host.App.AddLike(501, "Лола");

        await host.SyncAppAsync();

        host.Photos.Photos.Should().BeEmpty();
    }
}
