using CSharpFunctionalExtensions;
using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.Ports.Persistence;
using DateBot.Application.Ports.Services;
using DateBot.Application.UseCases.Commands;
using DateBot.Application.UseCases.Services;
using DateBot.Domain.Common.Errors;
using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases.Handlers;

/// <summary>Cards get buried in the chat, and cards from an older version lack newer buttons: this sends them anew.</summary>
internal sealed class OfferDraftsHandler(IMatchRepository matches, GenerateAndSendReply reply, IOwnerNotifier notifier)
    : ICommandHandler<OfferDraftsCommand, int>
{
    public Task<Result<int, Error>> HandleAsync(OfferDraftsCommand command, CancellationToken ct) =>
        matches.FindWithDraftAsync(ct).Bind(drafts => OfferAsync(drafts, ct));

    private async Task<Result<int, Error>> OfferAsync(IReadOnlyList<Match> drafts, CancellationToken ct)
    {
        foreach (var card in drafts.Select(reply.DraftCard).Where(card => card.HasValue))
        {
            var sent = await notifier.NotifyAsync(card.GetValueOrThrow(), ct);
            if (sent.IsFailure)
            {
                return sent.Error;
            }
        }

        return drafts.Count;
    }
}
