using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Application.Telegram;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.Extensions.Options;

namespace DateBot.Infrastructure.Platform;

/// <summary>Profile photos as files in the local data folder; nothing leaves the machine except the LLM call.</summary>
internal sealed class FilePhotoStore(IOptions<StorageOptions> options) : IPhotoStore
{
    public async Task<Result<Maybe<ProfilePhoto>, Error>> LoadAsync(PhotoFileId id, CancellationToken ct)
    {
        try
        {
            var path = PathOf(id);
            if (!File.Exists(path))
            {
                return Maybe<ProfilePhoto>.None;
            }

            var bytes = await File.ReadAllBytesAsync(path, ct);
            return Maybe.From(new ProfilePhoto(bytes, DetectMediaType(bytes)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<Maybe<ProfilePhoto>, Error>(ServiceErrors.Storage(ex.Message));
        }
    }

    public async Task<UnitResult<Error>> SaveAsync(PhotoFileId id, ProfilePhoto photo, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(options.Value.PhotosPath);
            await File.WriteAllBytesAsync(PathOf(id), photo.Content.ToArray(), ct);
            return UnitResult.Success<Error>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return UnitResult.Failure(ServiceErrors.Storage(ex.Message));
        }
    }

    public Task<UnitResult<Error>> DeleteAsync(PhotoFileId id, CancellationToken ct)
    {
        try
        {
            File.Delete(PathOf(id));
            return Task.FromResult(UnitResult.Success<Error>());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(UnitResult.Failure(ServiceErrors.Storage(ex.Message)));
        }
    }

    public static string DetectMediaType(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        [0x47, 0x49, 0x46, 0x38, ..] => "image/gif",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => "image/jpeg",
    };

    private string PathOf(PhotoFileId id) =>
        Path.Combine(options.Value.PhotosPath, id.Value.Replace(':', '_') + ".img");
}
