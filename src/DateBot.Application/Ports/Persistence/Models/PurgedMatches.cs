using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Ports.Persistence.Models;

/// <param name="Photos">Every photo of the deleted profiles, main and extra, for the photo store to delete.</param>
public sealed record PurgedMatches(int Count, IReadOnlyList<PhotoFileId> Photos);
