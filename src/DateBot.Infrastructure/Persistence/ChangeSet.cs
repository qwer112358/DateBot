using DateBot.Application.Processing;
using DateBot.Domain.Common;
using DateBot.Domain.Inbox;
using DateBot.Domain.Matches;
using DateBot.Domain.Sync;

namespace DateBot.Infrastructure.Persistence;

/// <summary>
/// Scoped list of aggregates loaded or created in this scope. The unit of work writes them back on commit
/// and publishes their domain events after the transaction.
/// </summary>
public sealed class ChangeSet
{
    private readonly Dictionary<Guid, MatchEntry> _matches = [];
    private readonly Dictionary<long, InboxEntry> _inbox = [];
    private readonly List<MatchRecord> _removedMatches = [];
    private readonly List<long> _removedInbox = [];

    public SyncCursor? Cursor { get; private set; }

    public ControlState? Control { get; private set; }

    public IEnumerable<MatchEntry> Matches => _matches.Values;

    public IEnumerable<InboxEntry> Inbox => _inbox.Values;

    public IReadOnlyList<MatchRecord> RemovedMatches => _removedMatches;

    public IReadOnlyList<long> RemovedInbox => _removedInbox;

    public IEnumerable<IAggregateRoot> Aggregates => _matches.Values.Select(e => (IAggregateRoot)e.Match);

    public Match? FindMatch(Guid id) => _matches.TryGetValue(id, out var entry) ? entry.Match : null;

    public Match? FindMatchByPerson(string personId) =>
        _matches.Values.FirstOrDefault(e => e.Match.PersonId.Value == personId)?.Match;

    public void Track(Match match, MatchRecord? record) => _matches[match.Id.Value] = new MatchEntry(match) { Record = record };

    public void Remove(Match match)
    {
        if (_matches.Remove(match.Id.Value, out var entry) && entry.Record is not null)
        {
            _removedMatches.Add(entry.Record);
        }
    }

    public InboxMessage? FindInbox(long id) => _inbox.TryGetValue(id, out var entry) ? entry.Message : null;

    public void Track(InboxMessage message, InboxRecord? record) => _inbox[message.Id.Value] = new InboxEntry(message) { Record = record };

    public void RemoveInbox(long id)
    {
        _inbox.Remove(id);
        _removedInbox.Add(id);
    }

    public void Track(SyncCursor cursor) => Cursor = cursor;

    public void Track(ControlState state) => Control = state;

    public void ClearRemovals()
    {
        _removedMatches.Clear();
        _removedInbox.Clear();
    }

    public sealed class MatchEntry(Match match)
    {
        public Match Match { get; } = match;

        public MatchRecord? Record { get; set; }
    }

    public sealed class InboxEntry(InboxMessage message)
    {
        public InboxMessage Message { get; } = message;

        public InboxRecord? Record { get; set; }
    }
}
