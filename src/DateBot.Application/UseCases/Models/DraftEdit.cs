using DateBot.Application.UseCases.Enums;

namespace DateBot.Application.UseCases.Models;

public sealed record DraftEdit(Guid MatchId, DraftEditKind Kind);
