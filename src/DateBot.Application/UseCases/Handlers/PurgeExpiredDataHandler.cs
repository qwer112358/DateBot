using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Processing.Options;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Models;
using DateBot.Domain.Common.Errors;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>Retention: removes data older than <see cref="ProcessingOptions.RetentionDays"/> (0 = keep forever).</summary>
internal sealed class PurgeExpiredDataHandler(
    IMatchRepository matches,
    IInboxRepository inbox,
    IPhotoStore photos,
    IClock clock,
    IOptions<ProcessingOptions> options)
    : ICommandHandler<PurgeExpiredDataCommand, PurgeReport>
{
    public async Task<Result<PurgeReport, Error>> HandleAsync(PurgeExpiredDataCommand command, CancellationToken ct)
    {
        if (options.Value.RetentionDays == 0)
        {
            return new PurgeReport(0, 0);
        }

        var before = clock.UtcNow.AddDays(-options.Value.RetentionDays);
        return await matches.PurgeReceivedBeforeAsync(before, ct)
            .Tap(async purged =>
            {
                foreach (var photo in purged.Photos)
                {
                    await photos.DeleteAsync(photo, ct);
                }
            })
            .Bind(purged => inbox.PurgeSentBeforeAsync(before, ct)
                .Map(inboxCount => new PurgeReport(purged.Count, inboxCount)));
    }
}
