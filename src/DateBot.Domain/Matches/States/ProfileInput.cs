using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.States;

/// <param name="ExtraPhotos">The profile's other photos after <paramref name="Photo"/>, shown to the owner only.</param>
public sealed record ProfileInput(
    ProfileName Name,
    Maybe<Age> Age,
    Maybe<City> City,
    Maybe<Bio> Bio,
    Interests Interests,
    Maybe<PhotoFileId> Photo,
    Maybe<ContactUsername> Username,
    string RawText,
    ProfileFacts? Facts = null,
    Maybe<AppProfileId> AppId = default,
    PhotoAlbum? ExtraPhotos = null);
