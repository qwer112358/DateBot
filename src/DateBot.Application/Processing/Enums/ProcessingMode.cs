namespace DateBot.Application.Processing.Enums;

public enum ProcessingMode
{
    /// <summary>Messages are sent automatically.</summary>
    Auto,

    /// <summary>Drafts go to the notifier bot; sending waits for your "Send".</summary>
    Confirm,
}
