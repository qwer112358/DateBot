using CSharpFunctionalExtensions;
using DateBot.Application.Processing.Enums;
using DateBot.Domain.Inbox.Enums;
using DateBot.Domain.Matches.Enums;

namespace DateBot.Application.UseCases.Models;

public sealed record StatusReport(
    IReadOnlyDictionary<InboxStatus, int> Inbox,
    IReadOnlyDictionary<MatchStatus, int> Matches,
    long Cursor,
    Maybe<DateTimeOffset> LastProcessedAt,
    bool Paused,
    ProcessingMode Mode,
    int ActionsLastHour,
    int MaxActionsPerHour);
