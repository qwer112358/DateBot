using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.Tests.Support;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.OwnerRequests;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.Tests.UseCases;

/// <summary>
/// «✏️ Изменить» and «🤖 Переписать» on a draft card: your next message is the new text or the hint for a rewrite,
/// and the card comes again with the result. /cancel or any other command leaves the draft as it was.
/// </summary>
public sealed class DraftEditTests
{
    private const string OwnText = "Привет, Лола! Тоже люблю горы. Куда ходила последний раз?";

    [Fact]
    public async Task Own_text_replaces_the_draft_and_is_what_gets_sent()
    {
        await using var host = await WithDraftAsync();
        var match = host.Store.SingleMatch;

        await host.OwnerAsync(new EditDraftRequest(7, 42, match.Id.Value, DraftEditKind.OwnText));
        await host.OwnerAsync(new UnknownRequest(7, 42, OwnText));

        host.Notifier.Sent.OfType<TextNotification>().Select(n => n.Text).Should().Contain(t => t.StartsWith("✏️"))
            .And.Contain(t => t.StartsWith("✅ Черновик обновлён"));
        var card = host.Notifier.Sent.OfType<DraftReadyNotification>().Last();
        card.Draft.Value.Should().Be(OwnText);
        card.Channels.Should().Equal(MessageChannel.App);
        host.Generator.Calls.Should().ContainSingle("your own text needs no model");

        await host.DecideAsync(match.Id.Value, approve: true, MessageChannel.App);

        host.App.Sent.Single().Text.Should().Be(OwnText);
    }

    [Fact]
    public async Task Text_that_breaks_the_rules_is_refused_and_the_edit_stays_open()
    {
        await using var host = await WithDraftAsync();
        var match = host.Store.SingleMatch;
        await host.OwnerAsync(new EditDraftRequest(7, 42, match.Id.Value, DraftEditKind.OwnText));

        await host.OwnerAsync(new UnknownRequest(7, 42, "Пиши в инсту instagram.com/me"));
        await host.OwnerAsync(new UnknownRequest(7, 42, OwnText));

        host.Notifier.Sent.OfType<TextNotification>().Select(n => n.Text)
            .Should().Contain(t => t.StartsWith("Не получилось") && t.Contains("ссылк") && t.Contains("/cancel"));
        match.DraftMessage.GetValueOrThrow().Text.GetValueOrThrow().Value.Should().Be(OwnText);
    }

    [Fact]
    public async Task Hint_rewrites_the_draft_with_the_model()
    {
        await using var host = await WithDraftAsync();
        var match = host.Store.SingleMatch;
        const string rewritten = "Привет, Лола! Горы и кофе — отличное сочетание. Какая вершина любимая?";
        host.Generator.Respond = _ => Result.Success<string, Error>(rewritten);

        await host.OwnerAsync(new EditDraftRequest(7, 42, match.Id.Value, DraftEditKind.Hint));
        await host.OwnerAsync(new UnknownRequest(7, 42, "короче и спроси про горы"));

        var (prompt, photos) = host.Generator.Calls.Last();
        prompt.User.Should().Contain("<draft>").And.Contain("Пожелание владельца: короче и спроси про горы").And.Contain("Люблю кофе и горы");
        photos.Should().NotBeEmpty();
        host.Notifier.Sent.OfType<DraftReadyNotification>().Last().Draft.Value.Should().Be(rewritten);
    }

    [Fact]
    public async Task Failed_rewrite_keeps_the_old_draft_instead_of_a_template()
    {
        await using var host = await WithDraftAsync();
        var match = host.Store.SingleMatch;
        var before = match.DraftMessage.GetValueOrThrow().Text.GetValueOrThrow();
        host.Generator.Respond = _ => Result.Failure<string, Error>(Error.External("llm.server_error", "Anthropic API временно недоступен"));

        await host.OwnerAsync(new EditDraftRequest(7, 42, match.Id.Value, DraftEditKind.Hint));
        await host.OwnerAsync(new UnknownRequest(7, 42, "короче"));

        match.DraftMessage.GetValueOrThrow().Text.GetValueOrThrow().Should().Be(before);
        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().Contain("временно недоступен");
    }

    [Fact]
    public async Task Cancel_leaves_the_draft_and_the_next_text_is_not_an_edit()
    {
        await using var host = await WithDraftAsync();
        var match = host.Store.SingleMatch;
        await host.OwnerAsync(new EditDraftRequest(7, 42, match.Id.Value, DraftEditKind.OwnText));

        await host.OwnerAsync(new CancelRequest(7, 42));
        await host.OwnerAsync(new UnknownRequest(7, 42, OwnText));

        host.Notifier.Sent.OfType<TextNotification>().Select(n => n.Text).Should().Contain("Оставил черновик как был.");
        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().StartWith("Команды:");
        match.DraftMessage.GetValueOrThrow().Text.GetValueOrThrow().Value.Should().NotBe(OwnText);
    }

    [Fact]
    public async Task Another_command_cancels_the_edit_and_still_runs()
    {
        await using var host = await WithDraftAsync();
        var draft = host.Store.SingleMatch.DraftMessage.GetValueOrThrow().Text;
        await host.OwnerAsync(new EditDraftRequest(7, 42, host.Store.SingleMatch.Id.Value, DraftEditKind.Hint));

        await host.OwnerAsync(new StatusRequest(7, 42));
        await host.OwnerAsync(new UnknownRequest(7, 42, OwnText));

        host.Notifier.Sent.OfType<StatusNotification>().Should().ContainSingle();
        host.Store.SingleMatch.DraftMessage.GetValueOrThrow().Text.Should().Be(draft, "the edit ended with /status");
    }

    [Fact]
    public async Task Drafts_command_sends_every_waiting_card_again()
    {
        await using var host = await WithDraftAsync();
        host.App.AddLike(502, "Мадина");
        await host.SyncAppAsync();
        await host.DecideAsync(host.Store.Matches.Values.Single(m => m.Profile.Name.Value == "Мадина").Id.Value, approve: false);

        await host.OwnerAsync(new DraftsRequest(7, 42));

        host.Notifier.Sent.OfType<DraftReadyNotification>().Select(n => n.Profile.Name.Value)
            .Should().Equal(["Лола", "Мадина", "Лола"], "the skipped draft does not come back");
        host.Notifier.Sent.OfType<TextNotification>().Last().Text.Should().Be("Черновиков ждут решения: 1, карточки выше.");
    }

    /// <summary>A like from the app, liked back, with its draft card waiting: the starting point of every edit.</summary>
    private static async Task<TestHost> WithDraftAsync()
    {
        var host = new TestHost(processing: TestOptions.TashdateProcessing(), parsing: TestOptions.TashdateParsing());
        host.App.AddLike(501, "Лола");
        await host.SyncAppAsync();
        host.Notifier.Sent.OfType<DraftReadyNotification>().Should().ContainSingle();
        return host;
    }
}
