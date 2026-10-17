using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Errors;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.MiniApp.Models;
using DateBot.Application.MiniApp.Options;
using DateBot.Application.Ports.Dating;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Processing.Options;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Models;
using DateBot.Domain.Common.Errors;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>
/// Reads the app's incoming likes and handles them one by one, in the app's order, each in its own scope. Known people
/// cost only a database lookup, so the whole list is walked; at most <see cref="MiniAppOptions.MaxLikesPerPass"/>
/// new people are answered or sent to you as cards per pass. The first retryable error (limit, pause, network) ends the pass.
/// </summary>
internal sealed class SyncAppLikesHandler(
    ITashdateApp app,
    IControlStateStore control,
    ICommandDispatcher dispatcher,
    IOptions<ProcessingOptions> options)
    : ICommandHandler<SyncAppLikesCommand, AppSyncReport>
{
    /// <summary>The app puts the likes you can open first; deeper pages are mostly hidden ones, not worth the requests.</summary>
    private const int MaxListed = 60;

    public Task<Result<AppSyncReport, Error>> HandleAsync(SyncAppLikesCommand command, CancellationToken ct) =>
        control.GetAsync(ct)
            .Ensure(state => !state.Paused, ServiceErrors.Paused)
            .Bind(_ => app.GetIncomingLikesAsync(MaxListed, ct))
            .Bind(likes => AnswerAsync(command, likes, ct));

    private async Task<Result<AppSyncReport, Error>> AnswerAsync(SyncAppLikesCommand command, AppIncomingLikes likes, CancellationToken ct)
    {
        var report = AppSyncReport.Start(likes.Open.Length, likes.Hidden);
        for (var i = 0; i < likes.Open.Length; i++)
        {
            if (report.Handled >= options.Value.MiniApp.MaxLikesPerPass)
            {
                return report with { Deferred = likes.Open.Length - i };
            }

            var outcome = await dispatcher.SendAsync<ProcessAppLikeCommand, ProcessOutcome>(new ProcessAppLikeCommand(likes.Open[i], command.ResendWaiting), ct);
            if (outcome.IsFailure)
            {
                return outcome.Error;
            }

            report = report.Add(outcome.Value);
        }

        return report;
    }
}
