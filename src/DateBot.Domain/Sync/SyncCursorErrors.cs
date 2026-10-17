using DateBot.Domain.Common;
using DateBot.Domain.Shared;

namespace DateBot.Domain.Sync;

public static class SyncCursorErrors
{
    public static Error MustMoveForward(long current, BotMessageId requested) => Error.Conflict(
        "sync_cursor.backwards", $"Курсор двигается только вперёд: сейчас {current}, запрошено {requested}");
}
