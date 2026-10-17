using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.Notifications;
using DateBot.Application.Processing.Enums;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.Tests.UseCases;

/// <summary>/matches: a draft card for every app match not yet written to through DateBot, never a sent message.</summary>
public sealed class MatchesCommandTests
{
    [Fact]
    public async Task Match_made_in_the_feed_gets_a_draft_with_the_album_even_in_auto_mode()
    {
        await using var host = Host(ProcessingMode.Auto);
        AddAppMatch(host, 777, "Аня", "anya_sample");

        await host.OwnerAsync(new MatchesRequest(7, 42));

        var draft = host.Notifier.Sent.OfType<DraftReadyNotification>().Single();
        draft.Mutual.Should().BeTrue();
        draft.WithAlbum.Should().BeTrue();
        draft.Channels.Should().Equal(MessageChannel.Telegram, MessageChannel.App);
        host.Client.SentToUsers.Should().BeEmpty("/matches only drafts");
        host.App.Sent.Should().BeEmpty();
        host.Store.SingleMatch.Kind.Should().Be(MatchKind.Mutual);
        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().Contain("Взаимных симпатий в приложении: 1").And.Contain("новых черновиков: 1");
    }

    [Fact]
    public async Task Person_already_written_to_is_left_alone()
    {
        await using var host = Host(ProcessingMode.Auto);
        host.App.AddLike(501, "Лола", username: "lola_tash");
        await host.SyncAppAsync();
        host.Client.SentToUsers.Should().ContainSingle();

        await host.OwnerAsync(new MatchesRequest(7, 42));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Should().BeEmpty();
        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().Contain("уже написано или пропущено: 1");
    }

    [Fact]
    public async Task Waiting_draft_comes_again_with_its_album()
    {
        await using var host = Host(ProcessingMode.Confirm);
        host.App.AddLike(501, "Лола", username: "lola_tash", extraPhotos: 1);
        await host.SyncAppAsync();

        await host.OwnerAsync(new MatchesRequest(7, 42));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Select(d => d.WithAlbum).Should().Equal(true, true);
        host.Generator.Calls.Should().ContainSingle("the waiting draft is not written again");
    }

    [Fact]
    public async Task Forgotten_person_gets_no_draft()
    {
        await using var host = Host(ProcessingMode.Confirm);
        AddAppMatch(host, 777, "Аня", "anya_sample");
        await host.OwnerAsync(new MatchesRequest(7, 42));
        await host.OwnerAsync(new ForgetRequest(7, 42, host.Store.SingleMatch.Id.Value));

        await host.OwnerAsync(new MatchesRequest(7, 42));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Should().ContainSingle();
        host.Store.Matches.Should().BeEmpty();
    }

    private static void AddAppMatch(TestHost host, long id, string name, string username)
    {
        host.App.AddLike(id, name, extraPhotos: 1);
        host.App.Likes.Clear();
        host.App.Matches.Add(new AppMatch(id, name, Maybe.From(username)));
    }

    private static TestHost Host(ProcessingMode mode) =>
        new(processing: TestOptions.TashdateProcessing(mode: mode), parsing: TestOptions.TashdateParsing());
}
