using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Domain.Common.Errors;

namespace DateBot.Application.Ports.Services;

/// <summary>Messages to you from the separate notifier bot (Bot API).</summary>
public interface IOwnerNotifier
{
    Task<UnitResult<Error>> NotifyAsync(OwnerNotification notification, CancellationToken ct);
}
