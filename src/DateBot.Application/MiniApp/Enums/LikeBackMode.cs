namespace DateBot.Application.MiniApp.Enums;

public enum LikeBackMode
{
    /// <summary>A new like in the app is liked back at once.</summary>
    Auto,

    /// <summary>A new like comes to the notifier bot as a card; the like back waits for your "Answer".</summary>
    Confirm,
}
