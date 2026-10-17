using CSharpFunctionalExtensions;
using DateBot.Application.UseCases.Models;

namespace DateBot.Application.UseCases.Services;

/// <summary>
/// The draft your next plain message to the notifier bot edits, after «✏️ Изменить» or «🤖 Переписать». Kept in
/// memory only: after a restart the card's buttons are gone anyway, and /cancel or any command brings the card back.
/// </summary>
public sealed class PendingDraftEdit
{
    private readonly Lock _gate = new();
    private Maybe<DraftEdit> _awaited;

    /// <returns>The edit this one replaces, if any.</returns>
    public Maybe<DraftEdit> Await(DraftEdit edit)
    {
        lock (_gate)
        {
            var previous = _awaited;
            _awaited = edit;
            return previous;
        }
    }

    public Maybe<DraftEdit> Take()
    {
        lock (_gate)
        {
            var awaited = _awaited;
            _awaited = Maybe<DraftEdit>.None;
            return awaited;
        }
    }
}
