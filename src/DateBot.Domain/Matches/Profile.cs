using CSharpFunctionalExtensions;
using FluentValidation;
using DateBot.Domain.Common;

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

/// <summary>Everything the Tashdate bot showed about a person, plus the original message text.</summary>
public sealed record Profile
{
    public const int MaxRawTextLength = 16_000;
    private static readonly ProfileInputValidator Validator = new();

    private Profile(ProfileInput input)
    {
        Name = input.Name;
        Age = input.Age;
        City = input.City;
        Bio = input.Bio;
        Interests = input.Interests;
        Photo = input.Photo;
        Username = input.Username;
        RawText = input.RawText;
    }

    public ProfileName Name { get; }

    public Maybe<Age> Age { get; }

    public Maybe<City> City { get; }

    public Maybe<Bio> Bio { get; }

    public Interests Interests { get; }

    public Maybe<PhotoFileId> Photo { get; }

    public Maybe<ContactUsername> Username { get; }

    public string RawText { get; }

    /// <summary>True when there is something besides the name to build a personal message on.</summary>
    public bool HasDetails => Bio.HasValue || !Interests.IsEmpty || Photo.HasValue;

    public static Result<Profile, Error> Create(ProfileInput input) =>
        Validator.ValidateAndCreate(input, "profile.invalid", i => new Profile(i));

    /// <summary>The same profile with the contact the bot revealed later (on a mutual like).</summary>
    public Profile WithUsername(ContactUsername username) =>
        new(new ProfileInput(Name, Age, City, Bio, Interests, Photo, username, RawText));
}

public sealed class ProfileInputValidator : AbstractValidator<ProfileInput>
{
    public ProfileInputValidator() =>
        RuleFor(p => p.RawText)
            .MaximumLength(Profile.MaxRawTextLength).WithErrorCode("profile.raw_text_too_long");
}
