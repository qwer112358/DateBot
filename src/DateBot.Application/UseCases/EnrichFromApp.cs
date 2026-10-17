using CSharpFunctionalExtensions;
using DateBot.Application.MiniApp;
using DateBot.Application.Parsing;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

/// <summary>
/// A match card shows only the name and @username. The app knows the whole profile and the app id, which the
/// message needs (details for the text, the in-app chat as a channel). Never blocks the message: on any problem
/// the card's data is used as is.
/// </summary>
public sealed class EnrichFromApp(ITashdateApp app, IOptions<ProcessingOptions> options, ILogger<EnrichFromApp> logger)
{
    public async Task EnrichAsync(Match match, CancellationToken ct)
    {
        if (!options.Value.MiniApp.Enabled || !match.IsMutual || match.Profile.AppId.HasValue)
        {
            return;
        }

        var enriched = await app.GetMatchesAsync(ct)
            .Map(matches => Find(matches, match.Profile))
            .Bind(found => found.HasValue
                ? AppProfileId.Create(found.GetValueOrThrow().ProfileId)
                    .Bind(id => app.GetProfileAsync(id, ct))
                    .Bind(profile => ProfileMapper.FromApp(profile))
                    .Bind(mapped => match.Enrich(mapped.Profile))
                    .Map(() => true)
                : Task.FromResult(Result.Success<bool, Error>(false)));

        if (enriched.IsFailure)
        {
            logger.LogWarning("Could not load the app profile of {Name}, using the match card only: {Error}", match.Profile.Name, enriched.Error);
        }
        else if (!enriched.Value)
        {
            logger.LogInformation("{Name} not found among the app matches; the in-app chat will not be offered", match.Profile.Name);
        }
    }

    /// <summary>By username; without one, by the name only when exactly one match has it (never guess between two).</summary>
    private static Maybe<AppMatch> Find(IReadOnlyCollection<AppMatch> matches, Profile profile)
    {
        if (profile.Username.HasValue)
        {
            var username = profile.Username.GetValueOrThrow().Value;
            return matches.TryFirst(m => m.Username.HasValue && string.Equals(m.Username.GetValueOrThrow().TrimStart('@'), username, StringComparison.OrdinalIgnoreCase));
        }

        var sameName = matches.Where(m => string.Equals(m.Name.Trim(), profile.Name.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        return sameName.Count == 1 ? sameName[0] : Maybe<AppMatch>.None;
    }
}
