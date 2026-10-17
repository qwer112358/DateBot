using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Ports.Dating;

/// <summary>
/// The Tashdate Mini App, used as your account: it signs in with the userbot's Telegram session.
/// Retryable failures end with <c>.unavailable</c>; a refusal means nothing was changed.
/// </summary>
public interface ITashdateApp
{
    /// <summary>Who liked you and is still waiting for an answer, in the app's order (the ones you can open come first).</summary>
    Task<Result<AppIncomingLikes, Error>> GetIncomingLikesAsync(int max, CancellationToken ct);

    Task<Result<AppProfile, Error>> GetProfileAsync(AppProfileId id, CancellationToken ct);

    Task<Result<ImmutableArray<AppMatch>, Error>> GetMatchesAsync(CancellationToken ct);

    /// <summary>Likes the profile; liking someone who liked you makes a match at once.</summary>
    Task<Result<AppLikeResult, Error>> LikeAsync(AppProfileId id, CancellationToken ct);

    /// <summary>
    /// Sends to the in-app chat (only open after a match). <paramref name="clientKey"/> lets the app drop a repeat.
    /// <see cref="AppErrors.SendOutcomeUnknown"/> means the text may have been delivered.
    /// </summary>
    Task<UnitResult<Error>> SendMessageAsync(AppProfileId to, ReplyText text, string clientKey, CancellationToken ct);

    /// <summary>A profile photo by its app file reference (<c>app:…</c>).</summary>
    Task<Result<Maybe<ProfilePhoto>, Error>> DownloadPhotoAsync(PhotoFileId photo, CancellationToken ct);
}
