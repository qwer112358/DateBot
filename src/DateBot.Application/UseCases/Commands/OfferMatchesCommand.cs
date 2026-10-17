using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.UseCases.Commands;

/// <summary>/matches: a draft card for every app match you have not written to through DateBot.</summary>
public sealed record OfferMatchesCommand : ICommand<MatchesReport>;
