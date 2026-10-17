using CSharpFunctionalExtensions;
using DateBot.Application.Parsing.Services;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Services;

/// <summary>
/// The person's photos, kept locally: every notification about them shows the main one, whether or not the LLM sees it,
/// and the first cards about them show the rest as an album. Taken from local storage, otherwise downloaded from the
/// bot message (Telegram) or the app and stored. Problems with a photo never block anything; they are logged and the
/// photo is simply missing.
/// </summary>
public sealed class ProfilePhotos(
    ITashdateClient client,
    ITashdateApp app,
    IPhotoStore photos,
    IOptions<ProcessingOptions> processing,
    ILogger<ProfilePhotos> logger)
{
    /// <summary>Stores every photo of the profile and returns the main one.</summary>
    public async Task<Maybe<ProfilePhoto>> GetAsync(Match match, CancellationToken ct)
    {
        foreach (var extra in match.Profile.ExtraPhotos.Items)
        {
            await GetAsync(match, extra, ct);
        }

        return match.Profile.Photo.HasValue ? await GetAsync(match, match.Profile.Photo.GetValueOrThrow(), ct) : Maybe<ProfilePhoto>.None;
    }

    private async Task<Maybe<ProfilePhoto>> GetAsync(Match match, PhotoFileId photoId, CancellationToken ct)
    {
        var photo = await photos.LoadAsync(photoId, ct)
            .Bind(stored => stored.HasValue
                ? Task.FromResult(Result.Success<Maybe<ProfilePhoto>, Error>(stored))
                : DownloadAsync(match, photoId, ct));

        if (photo.IsFailure)
        {
            logger.LogWarning("Photo of {Name} unavailable: {Error}", match.Profile.Name, photo.Error);
            return Maybe<ProfilePhoto>.None;
        }

        return photo.Value;
    }

    private Task<Result<Maybe<ProfilePhoto>, Error>> DownloadAsync(Match match, PhotoFileId photoId, CancellationToken ct)
    {
        var download = ProfileMapper.IsAppPhoto(photoId)
            ? processing.Value.MiniApp.Enabled ? app.DownloadPhotoAsync(photoId, ct) : NoPhoto()
            : match.SourceMessageId.HasValue ? client.DownloadPhotoAsync(match.SourceMessageId.GetValueOrThrow(), ct) : NoPhoto();
        return download.Tap(downloaded => downloaded.HasValue
            ? photos.SaveAsync(photoId, downloaded.GetValueOrThrow(), ct)
            : Task.CompletedTask);
    }

    private static Task<Result<Maybe<ProfilePhoto>, Error>> NoPhoto() =>
        Task.FromResult(Result.Success<Maybe<ProfilePhoto>, Error>(Maybe<ProfilePhoto>.None));
}
