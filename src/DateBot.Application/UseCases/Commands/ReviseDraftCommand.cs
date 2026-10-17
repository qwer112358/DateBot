using DateBot.Application.Abstractions.Messaging;
using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.Commands;

/// <param name="Input">Your new text, or the hint for the rewrite.</param>
public sealed record ReviseDraftCommand(Guid MatchId, DraftEditKind Kind, string Input) : ICommand<Unit>;
