using CSharpFunctionalExtensions;
using DateBot.Application.Notifications;
using DateBot.Application.Ports;
using DateBot.Domain.Common;

namespace DateBot.Application.Tests;

internal sealed class FakeNotifier : IOwnerNotifier
{
    public List<OwnerNotification> Sent { get; } = [];

    public Task<UnitResult<Error>> NotifyAsync(OwnerNotification notification, CancellationToken ct)
    {
        Sent.Add(notification);
        return Task.FromResult(UnitResult.Success<Error>());
    }
}
