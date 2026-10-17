namespace DateBot.Application.Processing;

/// <summary>When to send the message in scenario A (open question: does Tashdate allow it before a mutual like?).</summary>
public enum LikedMeMessagePolicy
{
    /// <summary>Right after the like back.</summary>
    AfterLikeBack,

    /// <summary>Only like back; the message goes when the mutual-like notification arrives.</summary>
    OnMutual,
}
