using DateBot.Application.Abstractions.Messaging;

namespace DateBot.Application.UseCases.Commands;

/// <summary>/drafts: every draft waiting for you comes again as a card; returns how many.</summary>
public sealed record OfferDraftsCommand : ICommand<int>;
