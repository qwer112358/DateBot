using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.Parsing.Models;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Parsing.Services;

/// <summary>Turns parsed text into domain value objects; collects every violation into one error.</summary>
public static class ProfileMapper
{
    private const int FingerprintBioPrefix = 100;
    private const string AppPhotoPrefix = "app:";

    public static Result<MappedProfile, Error> ToDomain(ParsedProfile parsed, string rawText)
    {
        var name = ProfileName.Create(parsed.Name);
        var age = Optional(parsed.Age, Age.Create);
        var city = Optional(parsed.City, City.Create);
        var bio = Optional(parsed.Bio, Bio.Create);
        var interests = Interests.Create(parsed.Interests);
        var facts = ProfileFacts.Create(parsed.Facts.IsDefault ? [] : parsed.Facts);
        var photo = Optional(parsed.PhotoId, PhotoFileId.Create);
        var username = Optional(parsed.Username, ContactUsername.Create);
        var appId = Optional(parsed.AppId, AppProfileId.Create);
        var personId = TashdateUserId.Create(PersonKey(parsed));

        Error[] errors =
        [
            .. ErrorsOf(name), .. ErrorsOf(age), .. ErrorsOf(city), .. ErrorsOf(bio), .. ErrorsOf(interests),
            .. ErrorsOf(facts), .. ErrorsOf(photo), .. ErrorsOf(username), .. ErrorsOf(appId), .. ErrorsOf(personId),
        ];

        return errors.Length > 0
            ? Result.Failure<MappedProfile, Error>(Combine(errors))
            : Profile
                .Create(new ProfileInput(
                    name.Value, age.Value, city.Value, bio.Value, interests.Value, photo.Value, username.Value, rawText, facts.Value, appId.Value))
                .Map(profile => new MappedProfile(personId.Value, profile));
    }

    /// <summary>A profile from the app: the app id is its identity, the photo is an app file.</summary>
    public static Result<MappedProfile, Error> FromApp(AppProfile profile) =>
        ToDomain(
            new ParsedProfile(
                profile.Name,
                profile.Age,
                profile.City,
                profile.Bio,
                profile.Interests,
                // An odd file name must not cost the whole profile: without the photo the message is still written.
                profile.PhotoFile.Map(file => AppPhotoPrefix + file).Where(id => PhotoFileId.Create(id).IsSuccess),
                Maybe<string>.None,
                Maybe<string>.None,
                profile.Facts,
                profile.Id),
            profile.RawText);

    public static bool IsAppPhoto(PhotoFileId photo) => photo.Value.StartsWith(AppPhotoPrefix, StringComparison.Ordinal);

    public static string AppPhotoFile(PhotoFileId photo) => photo.Value[AppPhotoPrefix.Length..];

    /// <summary>
    /// The app id when the profile came from the app; the bot id when the rules extract one; otherwise a fingerprint
    /// of name, age, city and the start of the first bio line, so that "liked you" and "mutual like" about the same
    /// person map to the same match even when the two notifications have different trailing lines. A name alone is
    /// too weak (a match card without @username shows nothing else, and names repeat), so the photo joins it then.
    /// </summary>
    public static string PersonKey(ParsedProfile parsed)
    {
        if (parsed.AppId.HasValue)
        {
            return PersonKeys.ForApp(parsed.AppId.GetValueOrThrow());
        }

        if (parsed.PersonId.HasValue)
        {
            return "id:" + parsed.PersonId.GetValueOrThrow().Trim();
        }

        var bio = parsed.Bio.GetValueOrDefault(string.Empty).Split('\n')[0];
        var nameOnly = parsed.Age.HasNoValue && parsed.City.HasNoValue && bio.Length == 0;
        var source = string.Join(
            "|",
            Normalize(parsed.Name),
            parsed.Age.HasValue ? parsed.Age.GetValueOrThrow().ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
            Normalize(parsed.City.GetValueOrDefault(string.Empty)),
            Normalize(bio.Length > FingerprintBioPrefix ? bio[..FingerprintBioPrefix] : bio))
            + (nameOnly && parsed.PhotoId.HasValue ? "|" + parsed.PhotoId.GetValueOrThrow() : string.Empty);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return "fp:" + Convert.ToHexStringLower(hash)[..20];
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static Result<Maybe<TVo>, Error> Optional<TIn, TVo>(Maybe<TIn> raw, Func<TIn, Result<TVo, Error>> create) =>
        raw.HasValue
            ? create(raw.GetValueOrThrow()).Map(value => Maybe.From(value))
            : Result.Success<Maybe<TVo>, Error>(Maybe<TVo>.None);

    private static IEnumerable<Error> ErrorsOf<T>(Result<T, Error> result) => result.IsFailure ? [result.Error] : [];

    private static Error Combine(Error[] errors) => Error.Validation(
        "profile.invalid",
        string.Join("; ", errors.Select(e => e.Message)),
        [.. errors.SelectMany(e => e.Details)]);
}
