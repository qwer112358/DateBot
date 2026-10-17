namespace DateBot.Application.Replies.Enums;

/// <summary>How much the model thinks before it answers; higher is slower and dearer.</summary>
public enum ModelEffort
{
    Low,
    Medium,
    High,
    Xhigh,
    Max,
}
