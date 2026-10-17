using DateBot.Application.MiniApp.Enums;

namespace DateBot.Application.MiniApp.Services;

/// <summary>
/// Asks the main loop to check the app's likes now rather than at the next scheduled time. The loop performs every
/// app action itself, so a like back you approved never races with a pass over the same person.
/// </summary>
public sealed class AppCheckSignal
{
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly Lock _gate = new();
    private AppCheckRequest _requested;

    public void Request(AppCheckRequest request)
    {
        lock (_gate)
        {
            _requested = request > _requested ? request : _requested;
            if (_wake.CurrentCount == 0)
            {
                _wake.Release();
            }
        }
    }

    /// <summary>Completes when a check is requested; <see cref="Take"/> says which.</summary>
    public Task WaitAsync(CancellationToken ct) => _wake.WaitAsync(ct);

    /// <summary>The strongest request since the last call, and clears it.</summary>
    public AppCheckRequest Take()
    {
        lock (_gate)
        {
            var requested = _requested;
            _requested = AppCheckRequest.None;
            _wake.Wait(0);
            return requested;
        }
    }
}
