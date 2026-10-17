using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Domain.Common;

namespace DateBot.Application.Ports;

/// <summary>Messages to you from the separate notifier bot (Bot API).</summary>
public interface IOwnerNotifier
{
    Task<UnitResult<Error>> NotifyAsync(OwnerNotification notification, CancellationToken ct);
}
