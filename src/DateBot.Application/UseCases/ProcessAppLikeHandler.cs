using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Parsing;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Matches;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

/// <summary>
/// One like from the app: find or create the match (the full profile is loaded only for a new person) → like back
/// through the app → the message step (a draft in Confirm mode). Retryable errors leave the like for the next pass;
/// other errors fail the match.
/// </summary>
internal sealed class ProcessAppLikeHandler(
    ITashdateApp app,
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    IClock clock,
    LikeBack likeBack,
    GenerateAndSendReply reply,
    ProfilePhotos photos,
    IOptions<ProcessingOptions> options,
    ILogger<ProcessAppLikeHandler> logger)
    : ICommandHandler<ProcessAppLikeCommand, ProcessOutcome>
{
    public Task<Result<ProcessOutcome, Error>> HandleAsync(ProcessAppLikeCommand command, CancellationToken ct) =>
        AppProfileId.Create(command.Like.ProfileId)
            .Bind(appId => FindAsync(appId, ct)
                .Bind(existing => existing.HasValue
                    ? ExecuteAsync(existing.GetValueOrThrow(), ct)
                    : AnswerNewAsync(appId, command.Like.Name, ct)));

    private Task<Result<Maybe<Match>, Error>> FindAsync(AppProfileId appId, CancellationToken ct) =>
        TashdateUserId.Create(PersonKeys.ForApp(appId.Value))
            .Bind(personId => matches.FindByPersonAsync(personId, ct))
            .Bind(byKey => byKey.HasValue
                ? Task.FromResult(Result.Success<Maybe<Match>, Error>(byKey))
                : matches.FindByContactAsync(Maybe<ContactUsername>.None, appId, ct));

    private Task<Result<ProcessOutcome, Error>> AnswerNewAsync(AppProfileId appId, string name, CancellationToken ct) =>
        matches.IsForgottenAsync([PersonKeys.ForApp(appId.Value)], ct)
            .Bind(forgotten => forgotten
                ? Ignore($"Данные о {name} были удалены, повторно не отвечаем")
                : LoadAsync(appId, name, ct)
                    .Bind(loaded => loaded.HasNoValue
                        ? Task.FromResult(Result.Success<ProcessOutcome, Error>(ProcessOutcome.Failed))
                        : CreateAsync(loaded.GetValueOrThrow(), ct)
                            .Bind(match => match.IsClosed
                                ? Ignore($"Анкета {match.Profile.Name} не прошла фильтры")
                                : ExecuteAsync(match, ct))));

    /// <summary>
    /// A profile the app no longer has or that cannot be read is skipped (None) instead of stopping the whole pass;
    /// only retryable errors stop it.
    /// </summary>
    private async Task<Result<Maybe<MappedProfile>, Error>> LoadAsync(AppProfileId appId, string name, CancellationToken ct)
    {
        var loaded = await app.GetProfileAsync(appId, ct).Bind(ProfileMapper.FromApp);
        if (loaded.IsSuccess)
        {
            return Maybe.From(loaded.Value);
        }

        if (ErrorClassifier.IsRetryable(loaded.Error))
        {
            return loaded.Error;
        }

        logger.LogWarning("App profile {AppId} ({Name}) skipped: {Error}", appId, name, loaded.Error);
        return Maybe<MappedProfile>.None;
    }

    /// <summary>A person the filters decline is still stored (declined), so the next pass does not load them again.</summary>
    private Task<Result<Match, Error>> CreateAsync(MappedProfile mapped, CancellationToken ct) =>
        MatchId.Create(Guid.CreateVersion7(clock.UtcNow))
            .Bind(id => Match.Create(new MatchInput(id, mapped.PersonId, Maybe.None, MatchKind.LikedMe, mapped.Profile, clock.UtcNow)))
            .Bind(match => (ProfileFilter.Accepts(match.Profile, options.Value.Filters) ? UnitResult.Success<Error>() : match.Decline())
                .Map(() => match))
            .Bind(match => matches.AddAsync(match, ct)
                .Bind(() => unitOfWork.CommitAsync(ct))
                .Tap(() => match.IsClosed ? Task.CompletedTask : photos.GetAsync(match, ct))
                .Map(() => match));

    private Task<Result<ProcessOutcome, Error>> ExecuteAsync(Match match, CancellationToken ct) =>
        ScenarioPlanner.Plan(MatchKind.LikedMe, Maybe.None, match) switch
        {
            ExecutePlan plan => RunStepsAsync(match, plan, ct),
            IgnorePlan ignore => Ignore(ignore.Reason),
            var other => Task.FromResult(Result.Failure<ProcessOutcome, Error>(
                Error.Unexpected("plan.unknown", $"Неизвестный план {other.GetType().Name}"))),
        };

    private async Task<Result<ProcessOutcome, Error>> RunStepsAsync(Match match, ExecutePlan plan, CancellationToken ct)
    {
        var steps = await (plan.LikeBack ? likeBack.ExecuteAsync(match, ct) : Task.FromResult(UnitResult.Success<Error>()))
            .Bind(() => plan.Message
                ? reply.ExecuteAsync(match, Maybe.None, ct)
                : Task.FromResult(Result.Success<ReplyOutcome, Error>(ReplyOutcome.NotNeeded)));

        if (steps.IsSuccess)
        {
            return steps.Value switch
            {
                ReplyOutcome.Drafted => ProcessOutcome.AwaitingConfirmation,
                ReplyOutcome.NotNeeded when !plan.LikeBack => ProcessOutcome.Ignored,
                _ => ProcessOutcome.Done,
            };
        }

        return ErrorClassifier.IsRetryable(steps.Error)
            ? steps.Error
            : await FailAsync(match, steps.Error, ct);
    }

    private async Task<Result<ProcessOutcome, Error>> FailAsync(Match match, Error error, CancellationToken ct)
    {
        logger.LogWarning("App like of {Name} failed: {Error}", match.Profile.Name, error);
        return await (match.IsClosed ? UnitResult.Success<Error>() : match.Fail(error, clock.UtcNow))
            .Bind(() => unitOfWork.CommitAsync(ct))
            .Map(() => ProcessOutcome.Failed);
    }

    private Task<Result<ProcessOutcome, Error>> Ignore(string reason)
    {
        logger.LogDebug("App like ignored: {Reason}", reason);
        return Task.FromResult(Result.Success<ProcessOutcome, Error>(ProcessOutcome.Ignored));
    }
}
