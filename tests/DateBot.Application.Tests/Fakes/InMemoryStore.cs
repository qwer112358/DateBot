using DateBot.Application.Processing.Models;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;
using DateBot.Domain.Sync;

namespace DateBot.Application.Tests.Fakes;

/// <summary>Shared in-memory "database"; aggregates are kept by reference, like a committed row.</summary>
internal sealed class InMemoryStore
{
    public Dictionary<Guid, Match> Matches { get; } = [];

    public Dictionary<long, InboxMessage> Inbox { get; } = [];

    public SyncCursor Cursor { get; set; } = SyncCursor.Start();

    public ControlState Control { get; set; } = ControlState.Initial with { OwnerUserId = 42L, OwnerChatId = 7L };

    public int Commits { get; set; }

    /// <summary>Tombstones of deleted people.</summary>
    public HashSet<string> Forgotten { get; } = [];

    public Match SingleMatch => Matches.Values.Single();
}
