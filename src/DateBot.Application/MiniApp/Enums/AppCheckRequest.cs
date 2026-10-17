namespace DateBot.Application.MiniApp.Enums;

/// <summary>Why the app's likes are checked before the scheduled time. A later value outranks an earlier one.</summary>
public enum AppCheckRequest
{
    None,

    /// <summary>You answered a like card: act on it now; the outcome comes as the usual notifications.</summary>
    Wake,

    /// <summary>/likes: check now and send a summary of what was found.</summary>
    Report,
}
