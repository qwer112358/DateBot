using CSharpFunctionalExtensions;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Ports.Services;

/// <summary>Local storage of downloaded profile photos.</summary>
public interface IPhotoStore
{
    Task<Result<Maybe<ProfilePhoto>, Error>> LoadAsync(PhotoFileId id, CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(PhotoFileId id, ProfilePhoto photo, CancellationToken ct);

    Task<UnitResult<Error>> DeleteAsync(PhotoFileId id, CancellationToken ct);
}
