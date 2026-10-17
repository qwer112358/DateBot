namespace DateBot.Application.UseCases.Models;

/// <param name="Total">Matches the app lists.</param>
/// <param name="Drafted">New drafts written now.</param>
/// <param name="Waiting">Drafts written earlier and sent to you again.</param>
/// <param name="Done">Already written to, skipped or forgotten.</param>
/// <param name="Deferred">Not reached this time because of the per-run limit; /matches again takes them.</param>
public sealed record MatchesReport(int Total, int Drafted, int Waiting, int Done, int Failed, int Deferred);
