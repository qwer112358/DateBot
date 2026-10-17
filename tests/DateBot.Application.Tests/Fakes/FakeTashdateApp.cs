using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Telegram.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Tests.Fakes;

/// <summary>The app as a list of likes, profiles and matches; liking someone who liked us makes a match.</summary>
internal sealed class FakeTashdateApp : ITashdateApp
{
    public List<AppLike> Likes { get; } = [];

    public int HiddenLikes { get; set; }

    public Dictionary<long, AppProfile> Profiles { get; } = [];

    public List<AppMatch> Matches { get; } = [];

    /// <summary>Username the app reveals when a like makes a match.</summary>
    public Dictionary<long, string> Usernames { get; } = [];

    public List<long> Liked { get; } = [];

    public List<(long To, string Text, string ClientKey)> Sent { get; } = [];

    public Queue<Error> LikeFailures { get; } = new();

    public Queue<Error> SendFailures { get; } = new();

    public int ProfileReads { get; private set; }

    public Task<Result<AppIncomingLikes, Error>> GetIncomingLikesAsync(int max, CancellationToken ct) =>
        Ok(new AppIncomingLikes([.. Likes.Take(max)], HiddenLikes));

    public Task<Result<AppProfile, Error>> GetProfileAsync(AppProfileId id, CancellationToken ct)
    {
        ProfileReads++;
        return Profiles.TryGetValue(id.Value, out var profile)
            ? Ok(profile)
            : Task.FromResult(Result.Failure<AppProfile, Error>(AppErrors.ProfileNotFound(id)));
    }

    public Error? MatchesFailure { get; set; }

    public Task<Result<ImmutableArray<AppMatch>, Error>> GetMatchesAsync(CancellationToken ct) =>
        MatchesFailure is { } error
            ? Task.FromResult(Result.Failure<ImmutableArray<AppMatch>, Error>(error))
            : Ok(Matches.ToImmutableArray());

    public Task<Result<AppLikeResult, Error>> LikeAsync(AppProfileId id, CancellationToken ct)
    {
        if (LikeFailures.TryDequeue(out var error))
        {
            return Task.FromResult(Result.Failure<AppLikeResult, Error>(error));
        }

        Liked.Add(id.Value);
        var liker = Likes.FirstOrDefault(l => l.ProfileId == id.Value);
        if (liker is null)
        {
            return Ok(new AppLikeResult(false, Maybe<string>.None));
        }

        // Like back on someone who liked us: they leave the likes list and become a match.
        Likes.Remove(liker);
        var username = Usernames.TryGetValue(id.Value, out var name) ? Maybe.From(name) : Maybe<string>.None;
        Matches.Add(new AppMatch(id.Value, liker.Name, username));
        return Ok(new AppLikeResult(true, username));
    }

    public Task<UnitResult<Error>> SendMessageAsync(AppProfileId to, ReplyText text, string clientKey, CancellationToken ct)
    {
        if (SendFailures.TryDequeue(out var error))
        {
            return Task.FromResult(UnitResult.Failure(error));
        }

        Sent.Add((to.Value, text.Value, clientKey));
        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<Result<Maybe<ProfilePhoto>, Error>> DownloadPhotoAsync(PhotoFileId photo, CancellationToken ct) =>
        Ok(Maybe.From(new ProfilePhoto(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg")));

    /// <summary>Someone liked us in the app; their full profile is behind the id.</summary>
    public void AddLike(long id, string name, string? bio = "Люблю кофе и горы", string? username = null)
    {
        Likes.Add(new AppLike(id, name));
        Profiles[id] = new AppProfile(
            id,
            name,
            Maybe.From(24),
            Maybe.From("Ташкент"),
            bio is null ? Maybe<string>.None : Maybe.From(bio),
            ["Кофе", "Походы"],
            ["Рост: 165 см", "Питомцы: кошка"],
            Maybe.From($"p{id}.jpg"),
            $"{name}, 24, Ташкент\n{bio}");
        if (username is not null)
        {
            Usernames[id] = username;
        }
    }

    private static Task<Result<T, Error>> Ok<T>(T value) => Task.FromResult(Result.Success<T, Error>(value));
}
