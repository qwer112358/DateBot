using CSharpFunctionalExtensions;
using DateBot.Application.Processing;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;

namespace DateBot.Application.UseCases;

public sealed record StatusReport(
    IReadOnlyDictionary<InboxStatus, int> Inbox,
    IReadOnlyDictionary<MatchStatus, int> Matches,
    long Cursor,
    Maybe<DateTimeOffset> LastProcessedAt,
    bool Paused,
    ProcessingMode Mode,
    int ActionsLastHour,
    int MaxActionsPerHour);
