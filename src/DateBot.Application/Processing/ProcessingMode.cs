namespace DateBot.Application.Processing;

public enum ProcessingMode
{
    /// <summary>Messages are sent automatically.</summary>
    Auto,

    /// <summary>Drafts go to the notifier bot; sending waits for your "Send".</summary>
    Confirm,
}
