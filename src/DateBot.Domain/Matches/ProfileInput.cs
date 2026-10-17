using CSharpFunctionalExtensions;

namespace DateBot.Domain.Matches;

public sealed record ProfileInput(
    ProfileName Name,
    Maybe<Age> Age,
    Maybe<City> City,
    Maybe<Bio> Bio,
    Interests Interests,
    Maybe<PhotoFileId> Photo,
    Maybe<ContactUsername> Username,
    string RawText);
