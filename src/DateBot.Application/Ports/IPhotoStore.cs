using CSharpFunctionalExtensions;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.Ports;

/// <summary>Local storage of downloaded profile photos.</summary>
public interface IPhotoStore
{
    Task<Result<Maybe<ProfilePhoto>, Error>> LoadAsync(PhotoFileId id, CancellationToken ct);

    Task<UnitResult<Error>> SaveAsync(PhotoFileId id, ProfilePhoto photo, CancellationToken ct);

    Task<UnitResult<Error>> DeleteAsync(PhotoFileId id, CancellationToken ct);
}
