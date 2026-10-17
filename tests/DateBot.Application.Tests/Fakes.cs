using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Notifications;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Application.Replies;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;
using DateBot.Domain.Shared;
using DateBot.Domain.Sync;

namespace DateBot.Application.Tests;

internal sealed class FakeTashdateClient : ITashdateClient
{
    public List<RawBotMessage> History { get; } = [];

    public List<(long MessageId, ImmutableArray<string> Labels)> Presses { get; } = [];

    public List<string> SentToBot { get; } = [];

    public List<(string Username, string Text)> SentToUsers { get; } = [];

    public Queue<Error> PressFailures { get; } = new();

    public Queue<Error> SendFailures { get; } = new();

    public Maybe<ProfilePhoto> Photo { get; set; } = Maybe<ProfilePhoto>.None;

    public int Downloads { get; private set; }

    public int Sends => SentToBot.Count + SentToUsers.Count;

    public Task<Result<long, Error>> ConnectAsync(CancellationToken ct) => Task.FromResult(Result.Success<long, Error>(42));

    public Task<Result<long, Error>> GetPositionBeforeAsync(DateTimeOffset since, CancellationToken ct) =>
        Task.FromResult(Result.Success<long, Error>(History.Where(m => m.SentAt < since).Select(m => m.Id).DefaultIfEmpty(0).Max()));

    public Task<Result<ImmutableArray<RawBotMessage>, Error>> GetHistoryAsync(long afterMessageId, int limit, CancellationToken ct) =>
        Task.FromResult(Result.Success<ImmutableArray<RawBotMessage>, Error>(
            [.. History.Where(m => m.Id > afterMessageId).OrderBy(m => m.Id).Take(limit)]));

    public Task<UnitResult<Error>> WaitForActivityAsync(TimeSpan timeout, CancellationToken ct) =>
        Task.FromResult(UnitResult.Success<Error>());

    public Task<UnitResult<Error>> PressButtonAsync(BotMessageId messageId, ImmutableArray<string> labels, CancellationToken ct)
    {
        if (PressFailures.TryDequeue(out var error))
        {
            return Task.FromResult(UnitResult.Failure(error));
        }

        Presses.Add((messageId.Value, labels));
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> SendToBotAsync(ReplyText text, CancellationToken ct)
    {
        if (SendFailures.TryDequeue(out var error))
        {
            return Task.FromResult(UnitResult.Failure(error));
        }

        SentToBot.Add(text.Value);
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> SendToUserAsync(ContactUsername username, ReplyText text, CancellationToken ct)
    {
        if (SendFailures.TryDequeue(out var error))
        {
            return Task.FromResult(UnitResult.Failure(error));
        }

        SentToUsers.Add((username.Value, text.Value));
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<Result<Maybe<ProfilePhoto>, Error>> DownloadPhotoAsync(BotMessageId messageId, CancellationToken ct)
    {
        Downloads++;
        return Task.FromResult(Result.Success<Maybe<ProfilePhoto>, Error>(Photo));
    }
}

/// <summary>Shared in-memory "database"; aggregates are kept by reference, like a committed row.</summary>
internal sealed class InMemoryStore
{
    public Dictionary<Guid, Match> Matches { get; } = [];

    public Dictionary<long, InboxMessage> Inbox { get; } = [];

    public SyncCursor Cursor { get; set; } = SyncCursor.Start();

    public ControlState Control { get; set; } = ControlState.Initial with { OwnerUserId = 42L, OwnerChatId = 7L };

    public int Commits { get; set; }

    public Match SingleMatch => Matches.Values.Single();
}

internal sealed class InMemoryRepositories(InMemoryStore store)
    : IMatchRepository, IInboxRepository, ISyncCursorRepository, IControlStateStore
{
    public Task<Result<Maybe<Match>, Error>> FindByIdAsync(MatchId id, CancellationToken ct) =>
        Ok(store.Matches.TryGetValue(id.Value, out var match) ? Maybe.From(match) : Maybe<Match>.None);

    public Task<Result<Maybe<Match>, Error>> FindByPersonAsync(TashdateUserId personId, CancellationToken ct) =>
        Ok(store.Matches.Values.TryFirst(m => m.PersonId == personId));

    public Task<UnitResult<Error>> AddAsync(Match match, CancellationToken ct)
    {
        store.Matches[match.Id.Value] = match;
        return Done();
    }

    public Task<UnitResult<Error>> RemoveAsync(Match match, CancellationToken ct)
    {
        store.Matches.Remove(match.Id.Value);
        return Done();
    }

    public Task<Result<IReadOnlyDictionary<MatchStatus, int>, Error>> CountByStatusAsync(CancellationToken ct) =>
        Ok<IReadOnlyDictionary<MatchStatus, int>>(store.Matches.Values.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<Result<IReadOnlyList<PhotoFileId>, Error>> PurgeReceivedBeforeAsync(DateTimeOffset before, CancellationToken ct) =>
        Ok<IReadOnlyList<PhotoFileId>>([]);

    public Task<Result<Maybe<InboxMessage>, Error>> FindAsync(BotMessageId id, CancellationToken ct) =>
        Ok(store.Inbox.TryGetValue(id.Value, out var message) ? Maybe.From(message) : Maybe<InboxMessage>.None);

    public Task<UnitResult<Error>> AddAsync(InboxMessage message, CancellationToken ct)
    {
        store.Inbox[message.Id.Value] = message;
        return Done();
    }

    public Task<UnitResult<Error>> RemoveAsync(BotMessageId id, CancellationToken ct)
    {
        store.Inbox.Remove(id.Value);
        return Done();
    }

    Task<Result<IReadOnlyDictionary<InboxStatus, int>, Error>> IInboxRepository.CountByStatusAsync(CancellationToken ct) =>
        Ok<IReadOnlyDictionary<InboxStatus, int>>(store.Inbox.Values.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<Result<int, Error>> PurgeSentBeforeAsync(DateTimeOffset before, CancellationToken ct) => Ok(0);

    Task<Result<SyncCursor, Error>> ISyncCursorRepository.GetAsync(CancellationToken ct) => Ok(store.Cursor);

    public Task<UnitResult<Error>> SaveAsync(SyncCursor cursor, CancellationToken ct)
    {
        store.Cursor = cursor;
        return Done();
    }

    Task<Result<ControlState, Error>> IControlStateStore.GetAsync(CancellationToken ct) => Ok(store.Control);

    public Task<UnitResult<Error>> SaveAsync(ControlState state, CancellationToken ct)
    {
        store.Control = state;
        return Done();
    }

    private static Task<Result<T, Error>> Ok<T>(T value) => Task.FromResult(Result.Success<T, Error>(value));

    private static Task<UnitResult<Error>> Done() => Task.FromResult(UnitResult.Success<Error>());
}

internal sealed class FakeUnitOfWork(InMemoryStore store, IDomainEventDispatcher events) : IUnitOfWork
{
    public async Task<UnitResult<Error>> CommitAsync(CancellationToken ct)
    {
        store.Commits++;
        await events.DispatchAsync([.. store.Matches.Values.SelectMany(m => m.PullDomainEvents())], ct);
        return UnitResult.Success<Error>();
    }
}

internal sealed class FakeNotifier : IOwnerNotifier
{
    public List<OwnerNotification> Sent { get; } = [];

    public Task<UnitResult<Error>> NotifyAsync(OwnerNotification notification, CancellationToken ct)
    {
        Sent.Add(notification);
        return Task.FromResult(UnitResult.Success<Error>());
    }
}

internal sealed class FakeReplyGenerator : IReplyGenerator
{
    public const string GoodReply = "Привет, Анна! Вижу, ты любишь горы. Какой поход запомнился тебе больше всего?";

    public Func<ReplyPrompt, Result<string, Error>> Respond { get; set; } = _ => Result.Success<string, Error>(GoodReply);

    public List<(ReplyPrompt Prompt, Maybe<ProfilePhoto> Photo)> Calls { get; } = [];

    public Task<Result<string, Error>> GenerateAsync(ReplyPrompt prompt, Maybe<ProfilePhoto> photo, CancellationToken ct)
    {
        Calls.Add((prompt, photo));
        return Task.FromResult(Respond(prompt));
    }
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        Delays.Add(delay);
        UtcNow += delay;
        return Task.CompletedTask;
    }
}

internal sealed class FakeJitter : IJitter
{
    public TimeSpan Next(TimeSpan min, TimeSpan max) => min;

    public int NextIndex(int count) => 0;
}

internal sealed class FakeRateLimiter : IRateLimiter
{
    public Queue<Error> Failures { get; } = new();

    public Task<UnitResult<Error>> CheckAsync(CancellationToken ct) =>
        Task.FromResult(Failures.TryDequeue(out var error) ? UnitResult.Failure(error) : UnitResult.Success<Error>());

    public Task<Result<int, Error>> UsedInLastHourAsync(CancellationToken ct) => Task.FromResult(Result.Success<int, Error>(0));
}

internal sealed class FakePhotoStore : IPhotoStore
{
    public Dictionary<string, ProfilePhoto> Photos { get; } = [];

    public Task<Result<Maybe<ProfilePhoto>, Error>> LoadAsync(PhotoFileId id, CancellationToken ct) =>
        Task.FromResult(Result.Success<Maybe<ProfilePhoto>, Error>(
            Photos.TryGetValue(id.Value, out var photo) ? Maybe.From(photo) : Maybe<ProfilePhoto>.None));

    public Task<UnitResult<Error>> SaveAsync(PhotoFileId id, ProfilePhoto photo, CancellationToken ct)
    {
        Photos[id.Value] = photo;
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> DeleteAsync(PhotoFileId id, CancellationToken ct)
    {
        Photos.Remove(id.Value);
        return Task.FromResult(UnitResult.Success<Error>());
    }
}
