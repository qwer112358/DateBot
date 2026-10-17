using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;
using DateBot.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Tests;

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

            match.StartLikeBack(Now).Bind(() => match.CompleteLikeBack(Now)).Bind(() => match.StartMessage(Reply, Now));
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

    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

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
