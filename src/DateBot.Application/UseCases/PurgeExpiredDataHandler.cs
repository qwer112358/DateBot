using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions;
using DateBot.Application.Ports;
using DateBot.Application.Processing;
using DateBot.Domain.Common;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases;

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
            .Tap(async purgedPhotos =>
            {
                foreach (var photo in purgedPhotos)
                {
                    await photos.DeleteAsync(photo, ct);
                }
            })
            .Bind(purgedPhotos => inbox.PurgeSentBeforeAsync(before, ct)
                .Map(inboxCount => new PurgeReport(purgedPhotos.Count, inboxCount)));
    }
}
