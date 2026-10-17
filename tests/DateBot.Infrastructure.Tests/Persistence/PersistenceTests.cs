using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Events;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Models;
using DateBot.Application.Processing.Options;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Primitives;
using DateBot.Domain.Inbox;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.Events;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;
using DateBot.Domain.Shared.ValueObjects;
using DateBot.Infrastructure.Persistence;
using DateBot.Infrastructure.Persistence.Records;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Tests.Persistence;

/// <summary>Real SQLite file with the real migrations, repositories and unit of work.</summary>
public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "datebot-tests", Guid.NewGuid().ToString("N"));
    private readonly CapturingDispatcher _events = new();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:DataDirectory"] = _directory })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddInfrastructure(configuration);
        services.AddSingleton<IDomainEventDispatcher>(_events);
        services.AddSingleton(Options.Create(new ProcessingOptions { MaxActionsPerHour = 2 }));
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(default);
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Match_survives_a_round_trip_and_events_are_published_after_commit()
    {
        var match = NewMatch(MatchKind.LikedMe);
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(match, default);
            _events.Received.Should().BeEmpty();
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
            _events.Received.Should().ContainSingle().Which.Should().BeOfType<LikeReceived>();

            match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now)).Bind(() => match.StartMessage(Reply, MessageChannel.Telegram, Now));
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
        });

        await InScopeAsync(async sp =>
        {
            var loaded = (await sp.GetRequiredService<IMatchRepository>().FindByPersonAsync(match.PersonId, default)).Value.GetValueOrThrow();

            loaded.Should().NotBeSameAs(match);
            loaded.Status.Should().Be(MatchStatus.LikedBack);
            loaded.Profile.Should().Be(match.Profile);
            loaded.Actions.Select(a => (a.Type, a.Status)).Should().Equal(
                (ActionType.LikeBack, ActionStatus.Done), (ActionType.SendMessage, ActionStatus.Pending));
            loaded.Actions[1].Text.Should().Be(Maybe.From(Reply));
            loaded.DomainEvents.Should().BeEmpty();

            loaded.CompleteMessage(Now).Bind(() => loaded.Close());
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
        });

        await InScopeAsync(async sp =>
        {
            var loaded = (await sp.GetRequiredService<IMatchRepository>().FindByIdAsync(match.Id, default)).Value.GetValueOrThrow();
            loaded.Status.Should().Be(MatchStatus.Done);
            (await sp.GetRequiredService<IMatchRepository>().CountByStatusAsync(default)).Value[MatchStatus.Done].Should().Be(1);
        });
    }

    [Fact]
    public async Task Inbox_cursor_and_control_flags_are_persisted_in_one_commit()
    {
        await InScopeAsync(async sp =>
        {
            var inbox = InboxMessage.Receive(MessageId(5), "Анна, 24", Now, Now).Value;
            var cursor = (await sp.GetRequiredService<ISyncCursorRepository>().GetAsync(default)).Value;
            inbox.Resolve(InboxStatus.Done, Maybe<string>.None, Now);
            cursor.Advance(MessageId(5), Now);

            await sp.GetRequiredService<IInboxRepository>().AddAsync(inbox, default);
            await sp.GetRequiredService<ISyncCursorRepository>().SaveAsync(cursor, default);
            await sp.GetRequiredService<IControlStateStore>().SaveAsync(new ControlState(true, 42L, 7L), default);
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
        });

        await InScopeAsync(async sp =>
        {
            (await sp.GetRequiredService<IInboxRepository>().FindAsync(MessageId(5), default)).Value.GetValueOrThrow().Status.Should().Be(InboxStatus.Done);
            var cursor = (await sp.GetRequiredService<ISyncCursorRepository>().GetAsync(default)).Value;
            cursor.Position.Should().Be(5);
            cursor.LastProcessedAt.Should().Be(Maybe.From(Now));
            (await sp.GetRequiredService<IControlStateStore>().GetAsync(default)).Value.Should().Be(new ControlState(true, 42L, 7L));
        });
    }

    [Fact]
    public async Task Corrupted_row_is_an_error_not_a_broken_object()
    {
        await InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DateBotDbContext>();
            db.Matches.Add(new MatchRecord
            {
                Id = Guid.NewGuid(),
                PersonId = "fp:broken",
                BotMessageId = 9,
                Name = "Лена",
                Age = 15,
                Kind = MatchKind.LikedMe,
                Status = MatchStatus.Received,
                ReceivedAt = Now,
                UpdatedAt = Now,
            });
            await db.SaveChangesAsync();
        });

        await InScopeAsync(async sp =>
        {
            var loaded = await sp.GetRequiredService<IMatchRepository>().FindByPersonAsync(TashdateUserId.Create("fp:broken").Value, default);

            loaded.IsFailure.Should().BeTrue();
            loaded.Error.Code.Should().Be("storage.corrupted");
        });
    }

    [Fact]
    public async Task Rate_limiter_counts_actions_of_the_last_hour()
    {
        await InScopeAsync(async sp =>
        {
            var clock = sp.GetRequiredService<IClock>();
            var first = NewMatch(MatchKind.LikedMe, "fp:one", 1);
            var second = NewMatch(MatchKind.LikedMe, "fp:two", 2);
            first.StartLikeBack(clock.UtcNow).Bind(() => first.CompleteLikeBack(clock.UtcNow));
            second.StartLikeBack(clock.UtcNow);
            await sp.GetRequiredService<IMatchRepository>().AddAsync(first, default);
            await sp.GetRequiredService<IMatchRepository>().AddAsync(second, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var limiter = sp.GetRequiredService<IRateLimiter>();
            var check = await limiter.CheckAsync(default);

            (await limiter.UsedInLastHourAsync(default)).Value.Should().Be(2);
            check.IsFailure.Should().BeTrue();
            check.Error.Type.Should().Be(ErrorType.RateLimited);
            check.Error.RetryAfter.GetValueOrThrow().Should().BeCloseTo(TimeSpan.FromHours(1), TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task Removing_a_match_removes_its_actions()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now);
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(match, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IMatchRepository>();
            var loaded = (await repository.FindByIdAsync(match.Id, default)).Value.GetValueOrThrow();
            await repository.RemoveAsync(loaded, default);
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
        });

        await InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DateBotDbContext>();
            (await db.Matches.CountAsync()).Should().Be(0);
            (await db.Actions.CountAsync()).Should().Be(0);
        });
    }

    [Fact]
    public async Task Unique_index_rejects_a_second_match_for_the_same_person()
    {
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(NewMatch(MatchKind.LikedMe, "fp:same", 1), default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(NewMatch(MatchKind.Mutual, "fp:same", 2), default);
            var commit = await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);

            commit.IsFailure.Should().BeTrue();
            commit.Error.Code.Should().Be("storage.failure");
        });
    }

    [Fact]
    public async Task Cursor_is_read_fresh_even_after_this_scope_saved_it()
    {
        await using var catchUp = _services.CreateAsyncScope();
        var cursors = catchUp.ServiceProvider.GetRequiredService<ISyncCursorRepository>();
        var start = (await cursors.GetAsync(default)).Value;
        start.Advance(MessageId(50), Now);
        await cursors.SaveAsync(start, default);
        await catchUp.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(default);

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<ISyncCursorRepository>();
            var cursor = (await repository.GetAsync(default)).Value;
            cursor.Advance(MessageId(61), Now);
            await repository.SaveAsync(cursor, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        (await cursors.GetAsync(default)).Value.Position.Should().Be(61);
    }

    [Fact]
    public async Task Forgetting_and_retention_leave_only_tombstones()
    {
        var forgotten = NewMatch(MatchKind.LikedMe, "fp:forget", 1);
        var expired = NewMatch(MatchKind.Mutual, "fp:expired", 2);
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(forgotten, default);
            await sp.GetRequiredService<IMatchRepository>().AddAsync(expired, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IMatchRepository>();
            var loaded = (await repository.FindByIdAsync(forgotten.Id, default)).Value.GetValueOrThrow();
            await repository.RemoveAsync(loaded, default);
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
            var purged = (await repository.PurgeReceivedBeforeAsync(Now.AddDays(1), default)).Value;
            purged.Count.Should().Be(1);
            purged.Photos.Should().ContainSingle();
        });

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IMatchRepository>();
            (await repository.IsForgottenAsync([forgotten.PersonId.Value], default)).Value.Should().BeTrue();
            (await repository.IsForgottenAsync([expired.PersonId.Value], default)).Value.Should().BeTrue();
            (await repository.IsForgottenAsync(["fp:someone-else"], default)).Value.Should().BeFalse();
            (await sp.GetRequiredService<DateBotDbContext>().Matches.CountAsync()).Should().Be(0);
        });
    }

    [Fact]
    public async Task Proposed_like_back_survives_a_restart_and_does_not_use_the_hourly_limit()
    {
        var match = AppLike(501);
        await InScopeAsync(async sp =>
        {
            match.ProposeLikeBack(sp.GetRequiredService<IClock>().UtcNow);
            await sp.GetRequiredService<IMatchRepository>().AddAsync(match, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var loaded = (await sp.GetRequiredService<IMatchRepository>().FindByIdAsync(match.Id, default)).Value.GetValueOrThrow();
            loaded.LikeBackProposal.HasValue.Should().BeTrue();
            (await sp.GetRequiredService<IRateLimiter>().UsedInLastHourAsync(default)).Value
                .Should().Be(0, "a card is not an action until you approve it");
        });
    }

    [Fact]
    public async Task Only_open_matches_with_a_waiting_draft_are_found_for_drafts()
    {
        var waiting = NewMatch(MatchKind.Mutual, "fp:waiting", 1);
        waiting.DraftReply(Reply, MessageId(1), Now);
        var skipped = NewMatch(MatchKind.Mutual, "fp:skipped", 2);
        skipped.DraftReply(Reply, MessageId(2), Now);
        skipped.SkipReply(Now);
        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IMatchRepository>();
            await repository.AddAsync(waiting, default);
            await repository.AddAsync(skipped, default);
            await repository.AddAsync(NewMatch(MatchKind.LikedMe, "fp:fresh", 3), default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var found = (await sp.GetRequiredService<IMatchRepository>().FindWithDraftAsync(default)).Value;

            found.Select(m => m.Id).Should().Equal(waiting.Id);
        });
    }

    [Fact]
    public async Task Draft_keeps_its_anchor_through_the_database()
    {
        var match = NewMatch(MatchKind.LikedMe);
        match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now)).Bind(() => match.DraftReply(Reply, MessageId(105), Now));
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(match, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var loaded = (await sp.GetRequiredService<IMatchRepository>().FindByIdAsync(match.Id, default)).Value.GetValueOrThrow();
            loaded.DraftMessage.GetValueOrThrow().Anchor.Should().Be(Maybe.From(MessageId(105)));
        });
    }

    [Fact]
    public async Task Like_from_the_app_round_trips_with_its_contacts_facts_and_channel()
    {
        var match = AppLike(501);
        match.StartLikeBack(Now)
            .Bind(() => match.RevealContact(Maybe.From(ContactUsername.Create("Lola_Tash").Value)))
            .Bind(() => match.ConfirmMutual())
            .Bind(() => match.CompleteLikeBack(Now))
            .Bind(() => match.StartMessage(Reply, MessageChannel.App, Now));
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(match, default);
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
        });

        await InScopeAsync(async sp =>
        {
            var loaded = (await sp.GetRequiredService<IMatchRepository>().FindByIdAsync(match.Id, default)).Value.GetValueOrThrow();

            loaded.SourceMessageId.HasNoValue.Should().BeTrue();
            loaded.IsMutual.Should().BeTrue();
            loaded.Profile.Should().Be(match.Profile);
            loaded.Profile.Facts.Items.Should().Equal("Рост: 165 см", "Питомцы: кошка");
            loaded.PendingAction.GetValueOrThrow().Channel.Should().Be(Maybe.From(MessageChannel.App));
        });
    }

    [Fact]
    public async Task Person_is_found_by_username_in_any_case_or_by_app_id()
    {
        var match = AppLike(501);
        match.RevealContact(Maybe.From(ContactUsername.Create("Lola_Tash").Value));
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(match, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IMatchRepository>();
            var byUsername = await repository.FindByContactAsync(Maybe.From(ContactUsername.Create("lola_tash").Value), Maybe<AppProfileId>.None, default);
            var byApp = await repository.FindByContactAsync(Maybe<ContactUsername>.None, Maybe.From(AppProfileId.Create(501).Value), default);
            var nobody = await repository.FindByContactAsync(Maybe<ContactUsername>.None, Maybe<AppProfileId>.None, default);

            byUsername.Value.GetValueOrThrow().Id.Should().Be(match.Id);
            byApp.Value.GetValueOrThrow().Id.Should().Be(match.Id);
            nobody.Value.HasNoValue.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Forgetting_leaves_a_tombstone_for_every_key_of_the_person()
    {
        var match = AppLike(501);
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IMatchRepository>().AddAsync(match, default);
            await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default);
        });

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IMatchRepository>();
            var loaded = (await repository.FindByIdAsync(match.Id, default)).Value.GetValueOrThrow();
            loaded.RevealContact(Maybe.From(ContactUsername.Create("lola_tash").Value));
            await repository.RemoveAsync(loaded, default);
            (await sp.GetRequiredService<IUnitOfWork>().CommitAsync(default)).IsSuccess.Should().BeTrue();
        });

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IMatchRepository>();
            (await repository.IsForgottenAsync(["app:501"], default)).Value.Should().BeTrue();
            (await repository.IsForgottenAsync(["tg:lola_tash"], default)).Value.Should().BeTrue("the username learned in the last scope counts too");
        });
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static Match AppLike(long appId) =>
        Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value,
            TashdateUserId.Create(PersonKeys.ForApp(appId)).Value,
            Maybe<BotMessageId>.None,
            MatchKind.LikedMe,
            Profile.Create(new ProfileInput(
                ProfileName.Create("Лола").Value,
                Maybe.From(Age.Create(23).Value),
                Maybe.From(City.Create("Ташкент").Value),
                Maybe<Bio>.None,
                Interests.Create(["Кофе"]).Value,
                Maybe.From(PhotoFileId.Create("app:p501.jpg").Value),
                Maybe<ContactUsername>.None,
                "Лола, 23, Ташкент",
                ProfileFacts.Create(["Рост: 165 см", "Питомцы: кошка"]).Value,
                Maybe.From(AppProfileId.Create(appId).Value),
                PhotoAlbum.Create([PhotoFileId.Create("app:p501-2.jpg").Value, PhotoFileId.Create("app:p501-3.jpg").Value]).Value)).Value,
            Now)).Value;

    private static readonly ReplyText Reply = ReplyText.Create("Привет! Вижу, ты тоже любишь горы.").Value;

    private static BotMessageId MessageId(long id) => BotMessageId.Create(id).Value;

    private static Match NewMatch(MatchKind kind, string personId = "fp:anna", long messageId = 100) =>
        Match.Create(new MatchInput(
            MatchId.Create(Guid.NewGuid()).Value,
            TashdateUserId.Create(personId).Value,
            MessageId(messageId),
            kind,
            Profile.Create(new ProfileInput(
                ProfileName.Create("Анна").Value,
                Maybe.From(Age.Create(24).Value),
                Maybe.From(City.Create("Ташкент").Value),
                Maybe.From(Bio.Create("Люблю горы").Value),
                Interests.Create(["горы", "кофе"]).Value,
                Maybe.From(PhotoFileId.Create("photo:111").Value),
                Maybe<ContactUsername>.None,
                "Анна, 24, Ташкент – Люблю горы")).Value,
            Now)).Value;

    private async Task InScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    private sealed class CapturingDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Received { get; } = [];

        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
        {
            Received.AddRange(events);
            return Task.CompletedTask;
        }
    }
}
