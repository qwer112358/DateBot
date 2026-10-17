using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.Parsing.Services;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Processing.Options;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Services;

/// <summary>
/// A match card shows only the name and @username (sometimes not even that). The app knows the whole profile and the
/// app id, which the message needs: details for the text and the in-app chat as a channel. A person the app does not
/// know is not an error; a failing app is, and the caller decides whether the message can go without it.
/// </summary>
public sealed class EnrichFromApp(ITashdateApp app, IOptions<ProcessingOptions> options, ILogger<EnrichFromApp> logger)
{
    public async Task<UnitResult<Error>> EnrichAsync(Match match, CancellationToken ct)
    {
        if (!options.Value.MiniApp.Enabled || !match.IsMutual || match.Profile.AppId.HasValue)
        {
            return UnitResult.Success<Error>();
        }

        var found = await app.GetMatchesAsync(ct).Map(matches => Find(matches, match.Profile));
        if (found.IsFailure)
        {
            return found.Error;
        }

        if (found.Value.HasNoValue)
        {
            logger.LogInformation("{Name} not found among the app matches; the in-app chat will not be offered", match.Profile.Name);
            return UnitResult.Success<Error>();
        }

        return await AppProfileId.Create(found.Value.GetValueOrThrow().ProfileId)
            .Bind(id => app.GetProfileAsync(id, ct))
            .Bind(ProfileMapper.FromApp)
            .Bind(mapped => match.Enrich(mapped.Profile));
    }

    /// <summary>
    /// By username. Without one, by the name among matches that have no username either, and only when exactly one
    /// fits: two people with the same name are never guessed between.
    /// </summary>
    private static Maybe<AppMatch> Find(IReadOnlyCollection<AppMatch> matches, Profile profile)
    {
        if (profile.Username.HasValue)
        {
            var username = profile.Username.GetValueOrThrow().Value;
            return matches.TryFirst(m => m.Username.HasValue && string.Equals(m.Username.GetValueOrThrow().TrimStart('@'), username, StringComparison.OrdinalIgnoreCase));
        }

        var sameName = matches
            .Where(m => m.Username.HasNoValue && string.Equals(m.Name.Trim(), profile.Name.Value, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return sameName.Count == 1 ? sameName[0] : Maybe<AppMatch>.None;
    }
}
