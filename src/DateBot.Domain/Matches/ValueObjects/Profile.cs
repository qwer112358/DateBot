using CSharpFunctionalExtensions;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Common.Validation;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.Validators;

namespace DateBot.Domain.Matches.ValueObjects;

/// <summary>Everything known about a person: what the bot or the app showed, plus the original text.</summary>
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
        Facts = input.Facts ?? ProfileFacts.Empty;
        AppId = input.AppId;
    }

    public ProfileName Name { get; }

    public Maybe<Age> Age { get; }

    public Maybe<City> City { get; }

    public Maybe<Bio> Bio { get; }

    public Interests Interests { get; }

    public ProfileFacts Facts { get; }

    public Maybe<PhotoFileId> Photo { get; }

    /// <summary>Telegram username for direct messages.</summary>
    public Maybe<ContactUsername> Username { get; }

    /// <summary>Profile id in the dating app, for the in-app like and chat.</summary>
    public Maybe<AppProfileId> AppId { get; }

    public string RawText { get; }

    /// <summary>True when there is something besides the name to build a personal message on.</summary>
    public bool HasDetails => Bio.HasValue || !Interests.IsEmpty || !Facts.IsEmpty || Photo.HasValue;

    public static Result<Profile, Error> Create(ProfileInput input) =>
        Validator.ValidateAndCreate(input, "profile.invalid", i => new Profile(i));

    /// <summary>The same profile with contacts learned later (a match card, the app's like response).</summary>
    public Profile WithContact(Maybe<ContactUsername> username, Maybe<AppProfileId> appId) =>
        new(ToInput() with { Username = Username.Or(username), AppId = AppId.Or(appId) });

    /// <summary>Fills the fields this profile lacks from a fuller one (the app profile behind a short match card).</summary>
    public Profile Enrich(Profile details) =>
        new(ToInput() with
        {
            Age = Age.Or(details.Age),
            City = City.Or(details.City),
            Bio = Bio.Or(details.Bio),
            Interests = Interests.IsEmpty ? details.Interests : Interests,
            Facts = Facts.IsEmpty ? details.Facts : Facts,
            Photo = Photo.Or(details.Photo),
            Username = Username.Or(details.Username),
            AppId = AppId.Or(details.AppId),
        });

    private ProfileInput ToInput() => new(Name, Age, City, Bio, Interests, Photo, Username, RawText, Facts, AppId);
}
