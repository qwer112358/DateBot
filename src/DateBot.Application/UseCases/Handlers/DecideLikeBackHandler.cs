using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Enums;
using DateBot.Application.MiniApp.Services;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>
/// Your answer on a like card. Yes makes the like back Pending and wakes the main loop, which does it as it does any
/// unfinished like back. No closes the person; the like stays in the app for a manual answer.
/// </summary>
internal sealed class DecideLikeBackHandler(
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    AppCheckSignal appCheck,
    IClock clock)
    : ICommandHandler<DecideLikeBackCommand, Unit>
{
    public Task<Result<Unit, Error>> HandleAsync(DecideLikeBackCommand command, CancellationToken ct) =>
        MatchId.Create(command.MatchId)
            .Bind(id => matches.FindByIdAsync(id, ct).Bind(found => found.ToResult(MatchErrors.NotFound(id))))
            .Bind(match => Decide(match, command.Approve).Bind(() => unitOfWork.CommitAsync(ct)))
            .TapIf(command.Approve, () => appCheck.Request(AppCheckRequest.Wake))
            .Map(() => Unit.Value);

    private UnitResult<Error> Decide(Match match, bool approve) =>
        approve
            ? match.LikeBackProposal.ToResult(MatchErrors.NoLikeBackProposal).Bind(_ => match.StartLikeBack(clock.UtcNow))
            : match.SkipLikeBack(clock.UtcNow);
}
