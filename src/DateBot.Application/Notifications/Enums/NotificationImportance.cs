namespace DateBot.Application.Notifications.Enums;

public enum NotificationImportance
{
    /// <summary>Sent only with verbosity <c>All</c>.</summary>
    Info,

    /// <summary>Needs your decision (Confirm mode); always sent.</summary>
    Actionable,

    /// <summary>Errors and pauses; always sent.</summary>
    Error,

    /// <summary>Answer to your command; always sent.</summary>
    Reply,
}
