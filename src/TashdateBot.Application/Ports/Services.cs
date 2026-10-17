using CSharpFunctionalExtensions;
using TashdateBot.Application.Notifications;
using TashdateBot.Application.Replies;
using TashdateBot.Application.Telegram;
using TashdateBot.Domain.Common;
using TashdateBot.Domain.Matches;

namespace TashdateBot.Application.Ports;

/// <summary>Messages to you from the separate notifier bot (Bot API).</summary>
public interface IOwnerNotifier
{
    Task<UnitResult<Error>> NotifyAsync(OwnerNotification notification, CancellationToken ct);
}

/// <summary>LLM that writes the first message.</summary>
public interface IReplyGenerator
{
    Task<Result<string, Error>> GenerateAsync(ReplyPrompt prompt, Maybe<ProfilePhoto> photo, CancellationToken ct);
}

/// <summary>Local storage of downloaded profile photos.</summary>
public interface IPhotoStore
{
    Task<Result<Maybe<ProfilePhoto>, Error>> LoadAsync(PhotoFileId id, CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(PhotoFileId id, ProfilePhoto photo, CancellationToken ct);

    Task<UnitResult<Error>> DeleteAsync(PhotoFileId id, CancellationToken ct);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    Task DelayAsync(TimeSpan delay, CancellationToken ct);
}

public interface IJitter
{
    TimeSpan Next(TimeSpan min, TimeSpan max);

    int NextIndex(int count);
}

/// <summary>Hourly limit of outgoing actions (likes and messages).</summary>
public interface IRateLimiter
{
    /// <summary>Fails with a RateLimited error carrying <c>RetryAfter</c> when the limit is reached.</summary>
    Task<UnitResult<Error>> CheckAsync(CancellationToken ct);

    Task<Result<int, Error>> UsedInLastHourAsync(CancellationToken ct);
}
