using CSharpFunctionalExtensions;
using DateBot.Application.Processing;
using DateBot.Domain.Common;

namespace DateBot.Application.Ports;

/// <summary>Paused flag and owner identity, persisted so that /stop survives a restart.</summary>
public interface IControlStateStore
{
    Task<Result<ControlState, Error>> GetAsync(CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(ControlState state, CancellationToken ct);
}
