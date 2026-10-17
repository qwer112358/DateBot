using DateBot.Application.Abstractions.Messaging;

namespace DateBot.Application.UseCases.Commands;

/// <summary>Sends the draft card again: an edit was cancelled after its press had removed the old card's buttons.</summary>
public sealed record OfferDraftCommand(Guid MatchId) : ICommand<Unit>;
