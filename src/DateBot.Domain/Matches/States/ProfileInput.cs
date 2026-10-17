using CSharpFunctionalExtensions;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Domain.Matches.States;

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
    Maybe<AppProfileId> AppId = default);
