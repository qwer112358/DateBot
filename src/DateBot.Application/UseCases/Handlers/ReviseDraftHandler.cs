using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.Replies.Options;
using DateBot.Application.Replies.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Enums;
using DateBot.Application.UseCases.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;
using DateBot.Domain.Matches.ValueObjects;
using Microsoft.Extensions.Options;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>
/// Replaces the draft with your text, or with the model's rewrite by your hint. Your text passes the same rules as any
/// message (no links, phone numbers or @mentions). The new card comes from the <c>ReplyDrafted</c> event.
/// </summary>
internal sealed class ReviseDraftHandler(
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    ReplyComposer composer,
    ProfilePhotos photos,
    IClock clock,
    IOptions<ReplyOptions> replies)
    : ICommandHandler<ReviseDraftCommand, Unit>
{
    public Task<Result<Unit, Error>> HandleAsync(ReviseDraftCommand command, CancellationToken ct) =>
        MatchId.Create(command.MatchId)
            .Bind(id => matches.FindByIdAsync(id, ct).Bind(found => found.ToResult(MatchErrors.NotFound(id))))
            .Bind(match => match.DraftMessage
                .ToResult(MatchErrors.NoDraft)
                .Bind(draft => command.Kind == DraftEditKind.OwnText
                    ? Task.FromResult(ReplyText.Create(command.Input))
                    : RewriteAsync(match, draft.Text.GetValueOrThrow(), command.Input, ct))
                .Bind(text => match.ReviseDraft(text, clock.UtcNow))
                .Bind(() => unitOfWork.CommitAsync(ct)))
            .Map(() => Unit.Value);

    private async Task<Result<ReplyText, Error>> RewriteAsync(Match match, ReplyText draft, string hint, CancellationToken ct)
    {
        var all = await photos.GetAllAsync(match, ct);
        return await composer.ReviseAsync(match.Profile, replies.Value.UsePhoto ? all : [], draft, hint, ct);
    }
}
