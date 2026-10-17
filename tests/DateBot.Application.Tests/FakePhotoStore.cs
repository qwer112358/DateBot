using CSharpFunctionalExtensions;
using DateBot.Application.Ports;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;

namespace DateBot.Application.Tests;

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
