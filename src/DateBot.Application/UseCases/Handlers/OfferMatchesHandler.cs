using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Errors;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.MiniApp.Options;
using DateBot.Application.Parsing.Services;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Models;
using DateBot.Application.UseCases.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.Enums;
using DateBot.Domain.Matches.States;
using DateBot.Domain.Matches.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>
/// Walks the app's matches and makes sure each one you have not written to has a draft card: a waiting draft comes
/// again, a person DateBot does not know yet (you liked them in the feed yourself) is loaded and gets a new one.
/// Drafts only, whatever the mode: nothing is sent from here. "Written to" means through DateBot; a message you typed
/// yourself is not visible to it. At most <see cref="MiniAppOptions.MaxLikesPerPass"/> new drafts per
/// run, since each one costs a model call; the first retryable error (limit, network) ends the run.
/// </summary>
internal sealed class OfferMatchesHandler(
    ITashdateApp app,
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    GenerateAndSendReply reply,
    ProfilePhotos photos,
    IOwnerNotifier notifier,
    IClock clock,
    IOptions<ProcessingOptions> options,
    ILogger<OfferMatchesHandler> logger)
    : ICommandHandler<OfferMatchesCommand, MatchesReport>
{
    private enum Offer
    {
        Drafted,
        Waiting,
        Done,
    }

    public Task<Result<MatchesReport, Error>> HandleAsync(OfferMatchesCommand command, CancellationToken ct) =>
        app.GetMatchesAsync(ct).Bind(list => OfferAllAsync(list, ct));

    private async Task<Result<MatchesReport, Error>> OfferAllAsync(IReadOnlyList<AppMatch> list, CancellationToken ct)
    {
        var report = new MatchesReport(list.Count, 0, 0, 0, 0, 0);
        for (var i = 0; i < list.Count; i++)
        {
            if (report.Drafted >= options.Value.MiniApp.MaxLikesPerPass)
            {
                return report with { Deferred = list.Count - i };
            }

            var offered = await OfferAsync(list[i], ct);
            if (offered.IsFailure && ErrorClassifier.IsRetryable(offered.Error))
            {
                return offered.Error;
            }

            if (offered.IsFailure)
            {
                logger.LogWarning("Match {AppId} ({Name}) not offered: {Error}", list[i].ProfileId, list[i].Name, offered.Error);
            }

            report = !offered.IsSuccess ? report with { Failed = report.Failed + 1 }
                : offered.Value == Offer.Drafted ? report with { Drafted = report.Drafted + 1 }
                : offered.Value == Offer.Waiting ? report with { Waiting = report.Waiting + 1 }
                : report with { Done = report.Done + 1 };
        }

        return report;
    }

    private Task<Result<Offer, Error>> OfferAsync(AppMatch appMatch, CancellationToken ct)
    {
        var username = appMatch.Username.Bind(name => ContactUsername.Create(name).IsSuccess
            ? Maybe.From(ContactUsername.Create(name).Value)
            : Maybe<ContactUsername>.None);
        return AppProfileId.Create(appMatch.ProfileId)
            .Bind(appId => FindAsync(appId, username, ct)
                .Bind(known => known.HasValue
                    ? OfferKnownAsync(known.GetValueOrThrow(), ct)
                    : OfferNewAsync(appId, username, ct)));
    }

    private Task<Result<Maybe<Match>, Error>> FindAsync(AppProfileId appId, Maybe<ContactUsername> username, CancellationToken ct) =>
        TashdateUserId.Create(PersonKeys.ForApp(appId.Value))
            .Bind(personId => matches.FindByPersonAsync(personId, ct))
            .Bind(byKey => byKey.HasValue
                ? Task.FromResult(Result.Success<Maybe<Match>, Error>(byKey))
                : matches.FindByContactAsync(username, appId, ct));

    private async Task<Result<Offer, Error>> OfferKnownAsync(Match match, CancellationToken ct)
    {
        if (reply.DraftCard(match).TryGetValue(out var card))
        {
            await notifier.NotifyAsync(card with { WithAlbum = true }, ct);
            return Offer.Waiting;
        }

        if (match.IsClosed || match.HasActiveMessage)
        {
            return Offer.Done;
        }

        // The app lists them as a match, so any channel that needs one is open now.
        return await match.ConfirmMutual()
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Bind(() => DraftAsync(match, ct));
    }

    private async Task<Result<Offer, Error>> OfferNewAsync(AppProfileId appId, Maybe<ContactUsername> username, CancellationToken ct)
    {
        string[] keys = username.HasValue
            ? [PersonKeys.ForApp(appId.Value), PersonKeys.ForTelegram(username.GetValueOrThrow().Value)]
            : [PersonKeys.ForApp(appId.Value)];
        var forgotten = await matches.IsForgottenAsync(keys, ct);
        if (forgotten.IsFailure || forgotten.Value)
        {
            return forgotten.IsFailure ? forgotten.Error : Offer.Done;
        }

        return await app.GetProfileAsync(appId, ct)
            .Bind(ProfileMapper.FromApp)
            .Bind(mapped => MatchId.Create(Guid.CreateVersion7(clock.UtcNow))
                .Bind(id => Match.Create(new MatchInput(id, mapped.PersonId, Maybe.None, MatchKind.Mutual, mapped.Profile, clock.UtcNow))))
            .Bind(match => match.RevealContact(username)
                .Bind(() => matches.AddAsync(match, ct))
                .Bind(() => unitOfWork.CommitAsync(ct))
                .Tap(() => photos.GetAllAsync(match, ct))
                .Bind(() => DraftAsync(match, ct)));
    }

    private Task<Result<Offer, Error>> DraftAsync(Match match, CancellationToken ct) =>
        reply.ExecuteAsync(match, Maybe.None, ct, draftOnly: true)
            .Map(outcome => outcome == ReplyOutcome.Drafted ? Offer.Drafted : Offer.Done);
}
